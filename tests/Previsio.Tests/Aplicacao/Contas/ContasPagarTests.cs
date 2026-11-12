using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Aplicacao.Contas;

public class ContasPagarTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    // 11/11/2026 09h em Brasília
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();

    private Cliente _rede = null!;
    private Empresa _centro = null!;
    private Empresa _norte = null!;

    private async Task CadastrarRede()
    {
        _rede = new Cliente();
        _rede.Nome = "Rede";
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _centro = Empresa("Auto Center Centro");
        _norte = Empresa("Auto Center Zona Norte");
        await _db.SaveChangesAsync();
    }

    private Empresa Empresa(string nome)
    {
        var empresa = new Empresa();
        empresa.ClienteId = _rede.Id;
        empresa.Nome = nome;
        empresa.Cnpj = "11222333000262";
        _db.Empresas.Add(empresa);
        return empresa;
    }

    private ContaPagarSincronizada Conta(Empresa empresa, long blingId, int dia, decimal valor,
        int situacao = SituacaoContaBling.EmAberto, string? fornecedor = null, long? contato = null)
    {
        var conta = new ContaPagarSincronizada();
        conta.EmpresaId = empresa.Id;
        conta.BlingId = blingId;
        conta.Vencimento = new DateTime(2026, 11, dia);
        conta.Valor = valor;
        conta.Situacao = situacao;
        conta.FornecedorNome = fornecedor;
        conta.BlingContatoId = contato;
        _db.ContasPagar.Add(conta);
        return conta;
    }

    private void NotaDaConta(Empresa empresa, string numero, string fornecedor, long blingId)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = _rede.Id;
        nota.EmpresaId = empresa.Id;
        nota.Numero = numero;
        nota.NomeFornecedor = fornecedor;
        nota.Status = StatusNota.Lancada;
        nota.Parcelas.Add(new ParcelaNota { NumeroParcela = "001", Valor = 100m, Vencimento = new DateTime(2026, 11, 20), BlingContaPagarId = blingId });
        _db.NotasFiscais.Add(nota);
    }

    private ContasPagarService Servico(SincronizacaoOptions? opcoes = null)
    {
        return ServicosDeTeste.ContasPagarService(_db, _relogio, Options.Create(opcoes ?? new SincronizacaoOptions()));
    }

    private async Task<PaginaContasPagarDto> Listar(FiltroContas filtro)
    {
        Resultado<PaginaContasPagarDto> resultado = await Servico().ListarAsync(_rede.Id, filtro, 1, CancellationToken.None);
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return resultado.Valor!;
    }

    // ---------- Listagem ----------

    [Fact]
    public async Task Lista_com_origem_nota_fornecedor_e_vencida()
    {
        await CadastrarRede();
        Conta(_centro, 1, 5, 300m);                                           // vencida, sem nome
        Conta(_centro, 2, 20, 100m, SituacaoContaBling.EmAberto, "ALFA DISTRIBUIDORA");
        Conta(_norte, 3, 15, 50m, SituacaoContaBling.EmAberto, "Energia Elétrica");
        Conta(_norte, 4, 12, 70m, SituacaoContaBling.Pago, "Aluguel");
        NotaDaConta(_centro, "48213", "ALFA DISTRIBUIDORA", 2);
        NotaDaConta(_norte, "999", "OUTRA", 2);                                 // mesma id em outra conta Bling: não liga
        await _db.SaveChangesAsync();

        PaginaContasPagarDto pagina = await Listar(new FiltroContas { Situacao = "Abertas" });

        Assert.Equal(3, pagina.Total);
        Assert.Equal(450m, pagina.ValorTotal);
        Assert.Equal(new long[] { 1, 3, 2 }, pagina.Itens.Select(i => i.BlingId).ToArray());
        ContaPagarDto vencida = pagina.Itens[0];
        Assert.True(vencida.Vencida);
        Assert.Equal("Bling", vencida.Origem);
        Assert.Null(vencida.Fornecedor);
        ContaPagarDto daNota = pagina.Itens[2];
        Assert.Equal("Previsio", daNota.Origem);
        Assert.Equal("48213", daNota.NumeroNota);
        Assert.Equal("001", daNota.NumeroParcela);
        Assert.Equal("ALFA DISTRIBUIDORA", daNota.Fornecedor);
        Assert.False(daNota.Vencida);
        Assert.Equal("Bling", pagina.Itens[1].Origem);
        Assert.Equal(30, pagina.DiasPassados);
        Assert.Equal(90, pagina.DiasFuturos);
    }

    [Fact]
    public async Task Filtros_de_situacao_origem_periodo_busca_e_empresa()
    {
        await CadastrarRede();
        Conta(_centro, 1, 5, 300m, SituacaoContaBling.EmAberto, "Contabilidade Silva");
        Conta(_centro, 2, 20, 100m, SituacaoContaBling.ParcialmentePago, "ALFA DISTRIBUIDORA");
        Conta(_norte, 3, 15, 50m, SituacaoContaBling.Cancelado, "Energia");
        Conta(_norte, 4, 12, 70m, SituacaoContaBling.Pago, "Aluguel");
        NotaDaConta(_centro, "48213", "ALFA DISTRIBUIDORA", 2);
        await _db.SaveChangesAsync();

        Assert.Equal(new long[] { 1 }, (await Listar(new FiltroContas { Situacao = "Vencidas" })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(new long[] { 4 }, (await Listar(new FiltroContas { Situacao = "pagas" })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(new long[] { 3 }, (await Listar(new FiltroContas { Situacao = "Canceladas" })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(4, (await Listar(new FiltroContas())).Total);
        Assert.Equal(new long[] { 2 }, (await Listar(new FiltroContas { Origem = "Previsio" })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(3, (await Listar(new FiltroContas { Origem = "Bling" })).Total);
        Assert.Equal(new long[] { 4, 3 }, (await Listar(new FiltroContas { De = new DateTime(2026, 11, 10), Ate = new DateTime(2026, 11, 15) })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(new long[] { 1 }, (await Listar(new FiltroContas { Busca = "silva" })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(new long[] { 2 }, (await Listar(new FiltroContas { Busca = "4821" })).Itens.Select(i => i.BlingId).ToArray());
        Assert.Equal(2, (await Listar(new FiltroContas { Busca = "zona norte" })).Total);
        Assert.Equal(2, (await Listar(new FiltroContas { EmpresaId = _centro.Id })).Total);
        Assert.Equal(new long[] { 1, 2, 4, 3 }, (await Listar(new FiltroContas { Ordem = "-valor" })).Itens.Select(i => i.BlingId).ToArray());
    }

    [Fact]
    public async Task Filtro_invalido_e_empresa_inativa()
    {
        await CadastrarRede();
        Conta(_centro, 1, 20, 300m);
        _norte.Ativo = false;
        Conta(_norte, 2, 20, 100m);
        await _db.SaveChangesAsync();

        Assert.Equal(1, (await Listar(new FiltroContas())).Total);
        ContasPagarService servico = Servico();
        Assert.Equal(TipoResultado.Invalido, (await servico.ListarAsync(_rede.Id, new FiltroContas { Situacao = "Quase" }, 1, CancellationToken.None)).Tipo);
        Assert.Equal(TipoResultado.Invalido, (await servico.ListarAsync(_rede.Id, new FiltroContas { Origem = "Email" }, 1, CancellationToken.None)).Tipo);
        Assert.Equal(TipoResultado.Invalido, (await servico.ListarAsync(_rede.Id, new FiltroContas { Ordem = "cor" }, 1, CancellationToken.None)).Tipo);
    }

    // ---------- Opções da sincronização ----------

    [Fact]
    public void Opcoes_fora_do_limite_voltam_ao_padrao_e_viram_agenda()
    {
        var opcoes = new SincronizacaoOptions();
        Assert.Equal("*/30 * * * *", opcoes.Cron());

        opcoes.IntervaloMinutos = 15;
        Assert.Equal("*/15 * * * *", opcoes.Cron());
        opcoes.IntervaloMinutos = 60;
        Assert.Equal("0 * * * *", opcoes.Cron());
        opcoes.IntervaloMinutos = 180;
        Assert.Equal("0 */3 * * *", opcoes.Cron());
        opcoes.IntervaloMinutos = 1440;
        Assert.Equal("0 0 * * *", opcoes.Cron());
        opcoes.IntervaloMinutos = 2;
        Assert.Equal("*/30 * * * *", opcoes.Cron());

        opcoes.DiasPassados = -1;
        opcoes.DiasFuturos = 0;
        opcoes.DiasRenovarFornecedor = 999;
        Assert.Equal(30, opcoes.DiasPassadosValidos());
        Assert.Equal(90, opcoes.DiasFuturosValidos());
        Assert.Equal(30, opcoes.DiasRenovarFornecedorValidos());
        opcoes.DiasPassados = 60;
        opcoes.DiasFuturos = 180;
        Assert.Equal(60, opcoes.DiasPassadosValidos());
        Assert.Equal(180, opcoes.DiasFuturosValidos());
        Assert.Equal(100, opcoes.MaximoContasAntigasValido());
        opcoes.MaximoContasAntigas = 0;
        Assert.Equal(100, opcoes.MaximoContasAntigasValido());
        opcoes.MaximoContasAntigas = 300;
        Assert.Equal(300, opcoes.MaximoContasAntigasValido());
    }

    // ---------- Nome do fornecedor ----------

    [Fact]
    public async Task Nome_vem_da_nota_ou_do_contato_do_Bling_guardado_e_renovado()
    {
        await CadastrarRede();
        ContaPagarSincronizada daNota = Conta(_centro, 1, 20, 100m, SituacaoContaBling.EmAberto, null, 900);
        ContaPagarSincronizada manual = Conta(_centro, 2, 20, 100m, SituacaoContaBling.EmAberto, null, 901);
        ContaPagarSincronizada outraManual = Conta(_centro, 3, 21, 100m, SituacaoContaBling.EmAberto, null, 901);
        ContaPagarSincronizada guardada = Conta(_centro, 4, 22, 100m, SituacaoContaBling.EmAberto, null, 902);
        ContaPagarSincronizada velha = Conta(_centro, 5, 22, 100m, SituacaoContaBling.EmAberto, null, 903);
        ContaPagarSincronizada sumiu = Conta(_centro, 6, 22, 100m, SituacaoContaBling.EmAberto, null, 904);
        NotaDaConta(_centro, "48213", "ALFA DISTRIBUIDORA", 1);
        DateTime agora = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        _db.FornecedoresBling.Add(new FornecedorBling { EmpresaId = _centro.Id, BlingContatoId = 902, Nome = "Contabilidade Silva", AtualizadoEm = agora.AddDays(-5) });
        _db.FornecedoresBling.Add(new FornecedorBling { EmpresaId = _centro.Id, BlingContatoId = 903, Nome = "Nome Antigo", AtualizadoEm = agora.AddDays(-40) });
        await _db.SaveChangesAsync();

        var respostas = new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (url.EndsWith("contatos/901"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":{\"id\":901,\"nome\":\" Energia Elétrica S.A. \",\"numeroDocumento\":\"12.345.678/0001-90\"}}");
            }
            if (url.EndsWith("contatos/903"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":{\"id\":903,\"nome\":\"Nome Novo\",\"numeroDocumento\":\"\"}}");
            }
            return RespostasFixas.Json(HttpStatusCode.NotFound, "{\"error\":{\"type\":\"RESOURCE_NOT_FOUND\"}}");
        });
        var servico = ServicosDeTeste.FornecedoresBlingService(_db, BlingDeTeste.Criar(respostas), NullLogger<FornecedoresBlingService>.Instance);
        var contas = new List<ContaPagarSincronizada> { daNota, manual, outraManual, guardada, velha, sumiu };

        int buscados = await servico.PreencherNomesAsync(_centro, "conta", "token", contas, 30, agora, CancellationToken.None);
        await _db.SaveChangesAsync();

        Assert.Equal(3, buscados);
        Assert.Equal(3, respostas.Chamadas.Count);
        Assert.Equal("ALFA DISTRIBUIDORA", daNota.FornecedorNome);
        Assert.Equal("Energia Elétrica S.A.", manual.FornecedorNome);
        Assert.Equal("Energia Elétrica S.A.", outraManual.FornecedorNome);
        Assert.Equal("Contabilidade Silva", guardada.FornecedorNome);
        Assert.Equal("Nome Novo", velha.FornecedorNome);
        Assert.Null(sumiu.FornecedorNome);
        FornecedorBling energia = _db.FornecedoresBling.Single(f => f.BlingContatoId == 901);
        Assert.Equal("12345678000190", energia.Documento);
        Assert.Equal(agora, energia.AtualizadoEm);

        // Na sincronização seguinte, o que já está guardado não gera chamada
        respostas.Chamadas.Clear();
        int deNovo = await servico.PreencherNomesAsync(_centro, "conta", "token", contas, 30, agora.AddMinutes(30), CancellationToken.None);
        Assert.Equal(1, deNovo);
        Assert.Single(respostas.Chamadas);
        Assert.EndsWith("contatos/904", respostas.Chamadas[0]);
    }
}
