using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Tests.Apoio;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Notas;
using Previsio.Core.Conferencia;
using Previsio.Aplicacao.Comum;
using Previsio.Infra.Seguranca;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Conferencia;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Aplicacao.Conferencia;

public class ConferenciaBlingTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private static NotaFiscal Nota(params (decimal valor, int dia)[] parcelas)
    {
        var nota = new NotaFiscal();
        nota.Numero = "48213";
        nota.DataEmissao = new DateTime(2026, 11, 6);
        nota.ValorTotal = parcelas.Sum(p => p.valor);
        int numero = 1;
        foreach ((decimal valor, int dia) in parcelas)
        {
            nota.Parcelas.Add(new ParcelaNota { NumeroParcela = numero.ToString("000"), Valor = valor, Vencimento = new DateTime(2026, 11, dia) });
            numero++;
        }
        return nota;
    }

    private static ContaNoBling Conta(long id, decimal valor, int dia, int situacao = SituacaoContaBling.EmAberto)
    {
        return new ContaNoBling { Id = id, ContatoId = 555, Valor = valor, Vencimento = new DateTime(2026, 11, dia), Situacao = situacao };
    }

    private static Task SemDetalhe(ContaNoBling conta)
    {
        return Task.CompletedTask;
    }

    // ---------- Regra de correspondência ----------

    [Fact]
    public async Task Todas_as_parcelas_achadas_com_a_tolerancia_de_valor_e_cada_conta_uma_vez()
    {
        NotaFiscal nota = Nota((3240m, 12), (3240m, 26));
        var contas = new List<ContaNoBling> { Conta(1, 3240.05m, 12), Conta(2, 3240m, 26), Conta(3, 3240m, 12, SituacaoContaBling.Cancelado) };

        ResultadoConferencia resultado = await ConferenciaBling.ConferirAsync(nota, contas, new RegrasConferencia(), SemDetalhe);

        Assert.True(resultado.Completa);
        Assert.Equal("1, 2", resultado.Contas());
    }

    [Fact]
    public async Task Parcial_nenhuma_e_tolerancia_de_dias()
    {
        NotaFiscal nota = Nota((1000m, 12), (1000m, 26));

        ResultadoConferencia parcial = await ConferenciaBling.ConferirAsync(nota, new List<ContaNoBling> { Conta(1, 1000m, 12) }, new RegrasConferencia(), SemDetalhe);
        Assert.True(parcial.Parcial);

        // Vencimento ajustado para o dia útil seguinte: só conta com tolerância de dias
        var contas = new List<ContaNoBling> { Conta(1, 1000m, 13), Conta(2, 1000m, 27) };
        Assert.True((await ConferenciaBling.ConferirAsync(nota, contas, new RegrasConferencia(), SemDetalhe)).Nenhuma);
        Assert.True((await ConferenciaBling.ConferirAsync(nota, contas, new RegrasConferencia { ToleranciaDias = 1 }, SemDetalhe)).Completa);

        // Diferença de R$ 0,20 passa da tolerância padrão (R$ 0,10)
        Assert.True((await ConferenciaBling.ConferirAsync(Nota((50m, 12)), new List<ContaNoBling> { Conta(1, 50.20m, 12) }, new RegrasConferencia(), SemDetalhe)).Nenhuma);
    }

    [Fact]
    public async Task Exigindo_o_numero_da_nota_busca_o_detalhe_e_confere_documento_ou_historico()
    {
        NotaFiscal nota = Nota((3240m, 12));
        var detalhes = new Dictionary<long, (string? doc, string? hist)>
        {
            { 1, ("99999", "NF 4821 outra nota") },
            { 2, (null, "Compra de peças NF 048213 Rota Sul") }
        };
        var buscadas = new List<long>();
        Func<ContaNoBling, Task> completar = conta =>
        {
            buscadas.Add(conta.Id);
            conta.NumeroDocumento = detalhes[conta.Id].doc;
            conta.Historico = detalhes[conta.Id].hist;
            return Task.CompletedTask;
        };

        ResultadoConferencia resultado = await ConferenciaBling.ConferirAsync(nota,
            new List<ContaNoBling> { Conta(1, 3240m, 12), Conta(2, 3240m, 12) }, new RegrasConferencia { ExigeNumeroNota = true }, completar);

        Assert.Equal("2", resultado.Contas());
        Assert.Equal(new List<long> { 1, 2 }, buscadas);
        Assert.True(ConferenciaBling.CitaNumero("48213/2", "48213"));
        Assert.False(ConferenciaBling.CitaNumero("148213", "48213"));
    }

    [Fact]
    public async Task Nota_sem_parcelas_procura_o_valor_total_em_ate_120_dias_da_emissao()
    {
        NotaFiscal nota = Nota();
        nota.ValorTotal = 907m;
        var contas = new List<ContaNoBling> { new ContaNoBling { Id = 7, Valor = 907m, Vencimento = new DateTime(2026, 12, 6), Situacao = SituacaoContaBling.Pago } };

        ResultadoConferencia resultado = await ConferenciaBling.ConferirAsync(nota, contas, new RegrasConferencia(), SemDetalhe);

        Assert.True(resultado.Completa);
        Assert.Null(resultado.Encontradas[0].Parcela);
    }

    // ---------- Antes de lançar ----------

    private async Task<NotaFiscal> CadastrarNota(bool conferir)
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        cliente.ConferirBlingAntesDeLancar = conferir;
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        var empresa = new Empresa();
        empresa.ClienteId = cliente.Id;
        empresa.Nome = "Filial Centro";
        empresa.Cnpj = "98765432000110";
        empresa.BlingAccessTokenProtegido = _protetor.Proteger("access-valido");
        empresa.BlingRefreshTokenProtegido = _protetor.Proteger("refresh-valido");
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddHours(2);
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        NotaFiscal nota = Nota((3240m, 12), (3240m, 26));
        nota.ClienteId = cliente.Id;
        nota.EmpresaId = empresa.Id;
        nota.ChaveAcesso = "24251112345678000199550010000482131000482130";
        nota.NomeFornecedor = "ROTA SUL PECAS LTDA";
        nota.CnpjFornecedor = "12345678000199";
        nota.CnpjDestinatario = empresa.Cnpj;
        _db.NotasFiscais.Add(nota);
        await _db.SaveChangesAsync();
        return nota;
    }

    private static RespostasFixas Bling(string contasNoBling)
    {
        return new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (requisicao.Method == HttpMethod.Get && url.Contains("contatos"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[{\"id\":555,\"nome\":\"ROTA SUL\"}]}");
            }
            if (requisicao.Method == HttpMethod.Get && url.Contains("contas/pagar"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[" + contasNoBling + "]}");
            }
            return RespostasFixas.Json(HttpStatusCode.Created, "{\"data\":{\"id\":9000}}");
        });
    }

    private LancamentoNotaService Job(RespostasFixas respostas)
    {
        BlingClient bling = BlingDeTeste.Criar(respostas);
        var tokens = ServicosDeTeste.TokenBlingService(bling, _db, _protetor, NullLogger<TokenBlingService>.Instance);
        return ServicosDeTeste.LancamentoNotaService(_db, bling, tokens, ServicosDeTeste.ConferenciaBlingService(_db, bling));
    }

    private const string Lancadas = "{\"id\":71,\"situacao\":1,\"vencimento\":\"2026-11-12\",\"valor\":3240,\"contato\":{\"id\":555}},"
        + "{\"id\":72,\"situacao\":2,\"vencimento\":\"2026-11-26\",\"valor\":3240,\"contato\":{\"id\":555}},"
        + "{\"id\":73,\"situacao\":1,\"vencimento\":\"2026-11-12\",\"valor\":3240,\"contato\":{\"id\":999}}";

    [Fact]
    public async Task Nota_ja_lancada_a_mao_nao_e_lancada_de_novo()
    {
        NotaFiscal nota = await CadastrarNota(true);
        RespostasFixas respostas = Bling(Lancadas);

        await Job(respostas).LancarAsync(nota.Id, CancellationToken.None);

        NotaFiscal depois = _db.NotasFiscais.Single();
        Assert.Equal(StatusNota.LancadaManualmente, depois.Status);
        Assert.Equal(ConferenciaBlingService.ResolvidaPor, depois.ResolvidaManualmentePor);
        Assert.Equal("Encontrada no Bling, lançada à mão (conta 71, 72).", depois.ObservacaoResolucao);
        Assert.Equal(new List<long?> { 71, 72 }, depois.Parcelas.OrderBy(p => p.Vencimento).Select(p => p.BlingContaPagarId).ToList());
        Assert.DoesNotContain(respostas.Chamadas, c => c.StartsWith("POST") && c.Contains("contas/pagar"));
        // As contas achadas já entram no painel, sem esperar a sincronização
        Assert.Equal(new long[] { 71, 72 }, _db.ContasPagar.OrderBy(c => c.BlingId).Select(c => c.BlingId).ToArray());
    }

    [Fact]
    public async Task Parcial_nao_lanca_e_fica_para_conferir()
    {
        NotaFiscal nota = await CadastrarNota(true);
        RespostasFixas respostas = Bling("{\"id\":71,\"situacao\":1,\"vencimento\":\"2026-11-12\",\"valor\":3240,\"contato\":{\"id\":555}}");

        await Job(respostas).LancarAsync(nota.Id, CancellationToken.None);

        NotaFiscal depois = _db.NotasFiscais.Single();
        Assert.Equal(StatusNota.Erro, depois.Status);
        Assert.Equal(RegrasLancamento.MaximoTentativas, depois.Tentativas);
        Assert.StartsWith("Achei 1 de 2 parcelas já lançadas no Bling (conta 71).", depois.MensagemErro);
        Assert.DoesNotContain(respostas.Chamadas, c => c.StartsWith("POST") && c.Contains("contas/pagar"));
    }

    [Fact]
    public async Task Com_a_conferencia_desligada_lanca_normalmente()
    {
        NotaFiscal nota = await CadastrarNota(false);
        RespostasFixas respostas = Bling(Lancadas);

        await Job(respostas).LancarAsync(nota.Id, CancellationToken.None);

        Assert.Equal(StatusNota.Lancada, _db.NotasFiscais.Single().Status);
        Assert.Equal(2, respostas.Chamadas.Count(c => c.StartsWith("POST") && c.Contains("contas/pagar")));
        Assert.DoesNotContain(respostas.Chamadas, c => c.StartsWith("GET") && c.Contains("contas/pagar"));
    }

    [Fact]
    public async Task Ao_lancar_grava_a_conta_no_Previsio_na_hora()
    {
        NotaFiscal nota = await CadastrarNota(false);
        // A sincronização já tinha trazido a 9002 (não duplica)
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = nota.EmpresaId!.Value, BlingId = 9002, Vencimento = new DateTime(2026, 11, 26), Valor = 3240m, Situacao = SituacaoContaBling.Pago
        });
        await _db.SaveChangesAsync();
        int proximoId = 9000;
        var respostas = new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (requisicao.Method == HttpMethod.Get && url.Contains("contatos"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[{\"id\":555,\"nome\":\"ROTA SUL\"}]}");
            }
            proximoId = proximoId + 1;
            return RespostasFixas.Json(HttpStatusCode.Created, "{\"data\":{\"id\":" + proximoId + "}}");
        });

        await Job(respostas).LancarAsync(nota.Id, CancellationToken.None);

        List<ContaPagarSincronizada> contas = _db.ContasPagar.OrderBy(c => c.BlingId).ToList();
        Assert.Equal(new long[] { 9001, 9002 }, contas.Select(c => c.BlingId).ToArray());
        Assert.Equal(new DateTime(2026, 11, 12), contas[0].Vencimento);
        Assert.Equal(3240m, contas[0].Valor);
        Assert.Equal(SituacaoContaBling.EmAberto, contas[0].Situacao);
        Assert.Equal(555, contas[0].BlingContatoId);
        Assert.Equal("ROTA SUL PECAS LTDA", contas[0].FornecedorNome);
        // A que já existia fica como a sincronização trouxe
        Assert.Equal(SituacaoContaBling.Pago, contas[1].Situacao);
    }

    // ---------- Pendentes depois da sincronização ----------

    [Fact]
    public async Task Pendente_encontrada_no_bling_fecha_sozinha()
    {
        NotaFiscal nota = await CadastrarNota(true);
        nota.Status = StatusNota.Erro;
        nota.Tentativas = RegrasLancamento.MaximoTentativas;
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = nota.EmpresaId!.Value, BlingId = 71, BlingContatoId = 555, Valor = 3240m, Vencimento = new DateTime(2026, 11, 12), Situacao = 1 });
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = nota.EmpresaId!.Value, BlingId = 72, BlingContatoId = 555, Valor = 3240m, Vencimento = new DateTime(2026, 11, 26), Situacao = 1 });
        await _db.SaveChangesAsync();
        Empresa empresa = _db.Empresas.Single();

        var servico = ServicosDeTeste.ConferenciaBlingService(_db, BlingDeTeste.Criar(Bling("")));
        await servico.FecharPendentesAsync(empresa, "conta", "token", CancellationToken.None);

        Assert.Equal(StatusNota.LancadaManualmente, _db.NotasFiscais.Single().Status);

        // Rede que prefere confirmar à mão: não fecha
        NotaFiscal outra = await CadastrarNota(true);
        outra.Status = StatusNota.Erro;
        Cliente rede = _db.Clientes.Single(c => c.Id == outra.ClienteId);
        rede.ConferenciaFecharPendentes = false;
        await _db.SaveChangesAsync();
        await servico.FecharPendentesAsync(_db.Empresas.Single(e => e.Id == outra.EmpresaId), "conta", "token", CancellationToken.None);
        Assert.Equal(StatusNota.Erro, _db.NotasFiscais.Single(n => n.Id == outra.Id).Status);
    }

    [Fact]
    public async Task Configuracao_valida_os_limites()
    {
        NotaFiscal nota = await CadastrarNota(true);
        var configuracao = ServicosDeTeste.ConfiguracaoConferenciaService(_db);

        var dados = new ConferenciaBlingDto { ConferirAntesDeLancar = true, ToleranciaValor = 6m, ToleranciaDias = 0, FecharPendentes = true };
        Assert.Equal(TipoResultado.Invalido, (await configuracao.AlterarAsync(nota.ClienteId, dados, CancellationToken.None)).Tipo);
        dados = new ConferenciaBlingDto { ConferirAntesDeLancar = true, ToleranciaValor = 1m, ToleranciaDias = 6, FecharPendentes = true };
        Assert.Equal(TipoResultado.Invalido, (await configuracao.AlterarAsync(nota.ClienteId, dados, CancellationToken.None)).Tipo);

        dados = new ConferenciaBlingDto { ConferirAntesDeLancar = false, ExigeNumeroNota = true, ToleranciaValor = 0.5m, ToleranciaDias = 2, FecharPendentes = false };
        Resultado<ConferenciaBlingDto> ok = await configuracao.AlterarAsync(nota.ClienteId, dados, CancellationToken.None);
        Assert.True(ok.Sucesso);
        Cliente rede = _db.Clientes.Single(c => c.Id == nota.ClienteId);
        Assert.False(rede.ConferirBlingAntesDeLancar);
        Assert.True(rede.ConferenciaExigeNumeroNota);
        Assert.Equal(0.5m, rede.ConferenciaToleranciaValor);
        Assert.Equal(2, rede.ConferenciaToleranciaDias);
        Assert.False(rede.ConferenciaFecharPendentes);
        Assert.True(new Cliente().ConferirBlingAntesDeLancar);
        Assert.Equal(0.10m, new Cliente().ConferenciaToleranciaValor);
    }
}
