using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Sincronizacao;

namespace Previsio.Api.Tests.Contas;

public class ContasAntigasEmAbertoTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly DateTime _agora = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private readonly DateTime _inicioJanela = new DateTime(2026, 10, 12);

    private async Task<Empresa> CadastrarEmpresa()
    {
        var rede = new Cliente { Nome = "Rede" };
        _db.Clientes.Add(rede);
        await _db.SaveChangesAsync();
        var empresa = new Empresa { ClienteId = rede.Id, Nome = "Loja", Cnpj = "11222333000181" };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa;
    }

    private ContaPagarSincronizada Conta(Empresa empresa, long blingId, DateTime vencimento, int situacao, DateTime conferidaEm)
    {
        var conta = new ContaPagarSincronizada
        {
            EmpresaId = empresa.Id, BlingId = blingId, Vencimento = vencimento, Valor = 100m,
            Situacao = situacao, SincronizadaEm = conferidaEm
        };
        _db.ContasPagar.Add(conta);
        return conta;
    }

    private static HttpResponseMessage Detalhe(long id, int situacao, string vencimento)
    {
        return RespostasFixas.Json(HttpStatusCode.OK,
            "{\"data\":{\"id\":" + id + ",\"situacao\":" + situacao + ",\"vencimento\":\"" + vencimento + "\",\"valor\":100}}");
    }

    [Fact]
    public async Task Confere_so_as_em_aberto_antes_da_janela_e_traz_paga_cancelada_e_excluida()
    {
        Empresa empresa = await CadastrarEmpresa();
        ContaPagarSincronizada paga = Conta(empresa, 1, new DateTime(2026, 9, 1), SituacaoContaBling.EmAberto, _agora.AddDays(-40));
        ContaPagarSincronizada parcial = Conta(empresa, 2, new DateTime(2026, 9, 5), SituacaoContaBling.ParcialmentePago, _agora.AddDays(-40));
        Conta(empresa, 3, new DateTime(2026, 9, 10), SituacaoContaBling.EmAberto, _agora.AddDays(-40));
        ContaPagarSincronizada aberta = Conta(empresa, 4, new DateTime(2026, 9, 12), SituacaoContaBling.EmAberto, _agora.AddDays(-40));
        Conta(empresa, 5, new DateTime(2026, 9, 1), SituacaoContaBling.Pago, _agora.AddDays(-40));        // já paga: não consulta
        Conta(empresa, 6, new DateTime(2026, 10, 20), SituacaoContaBling.EmAberto, _agora.AddDays(-1));  // dentro da janela: não consulta
        await _db.SaveChangesAsync();

        var respostas = new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (url.EndsWith("contas/pagar/1")) return Detalhe(1, SituacaoContaBling.Pago, "2026-09-01");
            if (url.EndsWith("contas/pagar/2")) return Detalhe(2, SituacaoContaBling.Cancelado, "2026-09-05");
            if (url.EndsWith("contas/pagar/4")) return Detalhe(4, SituacaoContaBling.EmAberto, "2026-09-12");
            return RespostasFixas.Json(HttpStatusCode.NotFound, "{\"error\":{\"type\":\"RESOURCE_NOT_FOUND\"}}");
        });
        var servico = ServicosDeTeste.ContasAntigasEmAberto(_db, BlingDeTeste.Criar(respostas), NullLogger<ContasAntigasEmAberto>.Instance);

        int conferidas = await servico.ConferirAsync(empresa, "conta", "token", _inicioJanela, 100, _agora, CancellationToken.None);
        await _db.SaveChangesAsync();

        Assert.Equal(4, conferidas);
        Assert.Equal(4, respostas.Chamadas.Count);
        Assert.Equal(SituacaoContaBling.Pago, paga.Situacao);
        Assert.Equal(SituacaoContaBling.Cancelado, parcial.Situacao);
        Assert.False(_db.ContasPagar.Any(c => c.BlingId == 3));
        Assert.Equal(SituacaoContaBling.EmAberto, aberta.Situacao);
        Assert.Equal(_agora, aberta.SincronizadaEm);
    }

    [Fact]
    public async Task Respeita_o_limite_comecando_pela_conferida_ha_mais_tempo_e_para_no_erro()
    {
        Empresa empresa = await CadastrarEmpresa();
        Conta(empresa, 1, new DateTime(2026, 9, 1), SituacaoContaBling.EmAberto, _agora.AddDays(-1));
        ContaPagarSincronizada maisAntiga = Conta(empresa, 2, new DateTime(2026, 9, 2), SituacaoContaBling.EmAberto, _agora.AddDays(-9));
        Conta(empresa, 3, new DateTime(2026, 9, 3), SituacaoContaBling.EmAberto, _agora.AddDays(-5));
        await _db.SaveChangesAsync();

        var respostas = new RespostasFixas(requisicao => Detalhe(2, SituacaoContaBling.Pago, "2026-09-02"));
        var servico = ServicosDeTeste.ContasAntigasEmAberto(_db, BlingDeTeste.Criar(respostas), NullLogger<ContasAntigasEmAberto>.Instance);
        Assert.Equal(1, await servico.ConferirAsync(empresa, "conta", "token", _inicioJanela, 1, _agora, CancellationToken.None));
        Assert.EndsWith("contas/pagar/2", respostas.Chamadas[0]);
        Assert.Equal(SituacaoContaBling.Pago, maisAntiga.Situacao);
        await _db.SaveChangesAsync();

        // Erro que não é "não encontrada" (ex.: limite do Bling): para e não mexe na conta
        var comErro = new RespostasFixas(requisicao => RespostasFixas.Json(HttpStatusCode.BadRequest, "{\"error\":{\"type\":\"VALIDATION_ERROR\"}}"));
        var servicoComErro = ServicosDeTeste.ContasAntigasEmAberto(_db, BlingDeTeste.Criar(comErro), NullLogger<ContasAntigasEmAberto>.Instance);
        Assert.Equal(0, await servicoComErro.ConferirAsync(empresa, "conta", "token", _inicioJanela, 100, _agora, CancellationToken.None));
        Assert.Single(comErro.Chamadas);
        Assert.Equal(2, _db.ContasPagar.Count(c => c.Situacao == SituacaoContaBling.EmAberto));
    }
}
