using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Tests.Apoio;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Bling;
using Previsio.Core.Comum;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Infra.Seguranca;
using Xunit;

namespace Previsio.Tests.Aplicacao.Sincronizacao;

public class SincronizacaoContasServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
    private readonly DateTime _hoje = RelogioBrasil.Hoje();

    private SincronizacaoContasService CriarServico(RespostasFixas respostas)
    {
        BlingClient bling = BlingDeTeste.Criar(respostas);
        var tokens = ServicosDeTeste.TokenBlingService(bling, _db, _protetor, NullLogger<TokenBlingService>.Instance);
        return ServicosDeTeste.SincronizacaoContasService(_db, bling, tokens, Options.Create(new SincronizacaoOptions()));
    }

    private async Task<Empresa> CadastrarEmpresa(Cliente rede, string cnpj, bool conectada)
    {
        var empresa = new Empresa { ClienteId = rede.Id, Nome = "Loja " + cnpj, Cnpj = cnpj };
        if (conectada)
        {
            empresa.BlingAccessTokenProtegido = _protetor.Proteger("access-valido");
            empresa.BlingRefreshTokenProtegido = _protetor.Proteger("refresh-valido");
            empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddHours(2);
        }
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa;
    }

    private async Task<Cliente> CadastrarRede(bool ativa)
    {
        var rede = new Cliente { Nome = "Rede", Contrato = SituacaoContrato.Contratado, Ativo = ativa };
        _db.Clientes.Add(rede);
        await _db.SaveChangesAsync();
        return rede;
    }

    private string Data(int dias)
    {
        return _hoje.AddDays(dias).ToString("yyyy-MM-dd");
    }

    private RespostasFixas BlingComContas(string contas)
    {
        return new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (url.Contains("contas/pagar?"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[" + contas + "]}");
            }
            if (url.Contains("contatos/"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":{\"id\":555,\"nome\":\"Fornecedor Beta\",\"numeroDocumento\":\"11222333000181\"}}");
            }
            return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[]}");
        });
    }

    [Fact]
    public async Task Sincronizar_atualiza_inclui_e_remove_as_contas_da_janela()
    {
        Cliente rede = await CadastrarRede(true);
        Empresa empresa = await CadastrarEmpresa(rede, "98765432000110", true);
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = empresa.Id, BlingId = 10, Vencimento = _hoje.AddDays(5), Valor = 100m, Situacao = SituacaoContaBling.EmAberto
        });
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = empresa.Id, BlingId = 11, Vencimento = _hoje.AddDays(6), Valor = 50m, Situacao = SituacaoContaBling.EmAberto
        });
        await _db.SaveChangesAsync();

        string contas = "{\"id\":10,\"situacao\":2,\"vencimento\":\"" + Data(5) + "\",\"valor\":120,\"contato\":{\"id\":555}},"
            + "{\"id\":12,\"situacao\":1,\"vencimento\":\"" + Data(20) + "\",\"valor\":300,\"contato\":{\"id\":555}}";

        await CriarServico(BlingComContas(contas)).SincronizarEmpresaAsync(empresa, CancellationToken.None);

        List<ContaPagarSincronizada> locais = _db.ContasPagar.OrderBy(c => c.BlingId).ToList();
        Assert.Equal(new long[] { 10, 12 }, locais.Select(c => c.BlingId).ToArray());
        Assert.Equal(120m, locais[0].Valor);
        Assert.Equal(SituacaoContaBling.Pago, locais[0].Situacao);
        Assert.Equal(_hoje.AddDays(20), locais[1].Vencimento);
        Assert.Equal(555, locais[1].BlingContatoId);
        Assert.NotNull(_db.Empresas.Single().UltimaSincronizacaoEm);
    }

    [Fact]
    public async Task Sincronizar_mantem_as_contas_fora_da_janela()
    {
        Cliente rede = await CadastrarRede(true);
        Empresa empresa = await CadastrarEmpresa(rede, "98765432000110", true);
        // Vence daqui a um ano: fora da janela de 90 dias, não some por não vir na lista
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = empresa.Id, BlingId = 30, Vencimento = _hoje.AddDays(365), Valor = 10m, Situacao = SituacaoContaBling.EmAberto
        });
        await _db.SaveChangesAsync();

        await CriarServico(BlingComContas("")).SincronizarEmpresaAsync(empresa, CancellationToken.None);

        Assert.Single(_db.ContasPagar.Where(c => c.BlingId == 30));
    }

    [Fact]
    public async Task Executar_so_sincroniza_empresas_conectadas_de_redes_que_processam()
    {
        Cliente ativa = await CadastrarRede(true);
        Cliente desativada = await CadastrarRede(false);
        Empresa conectada = await CadastrarEmpresa(ativa, "98765432000110", true);
        Empresa semBling = await CadastrarEmpresa(ativa, "11222333000181", false);
        Empresa daRedeDesativada = await CadastrarEmpresa(desativada, "33444555000199", true);

        await CriarServico(BlingComContas("")).ExecutarAsync(CancellationToken.None);

        Assert.NotNull(_db.Empresas.Single(e => e.Id == conectada.Id).UltimaSincronizacaoEm);
        Assert.Null(_db.Empresas.Single(e => e.Id == semBling.Id).UltimaSincronizacaoEm);
        Assert.Null(_db.Empresas.Single(e => e.Id == daRedeDesativada.Id).UltimaSincronizacaoEm);
    }

    [Fact]
    public async Task Executar_registra_falha_quando_o_Bling_recusa_a_renovacao_e_segue_para_a_proxima()
    {
        Cliente rede = await CadastrarRede(true);
        Empresa expirada = await CadastrarEmpresa(rede, "98765432000110", true);
        expirada.BlingTokenExpiraEm = DateTime.UtcNow.AddHours(-1);
        Empresa outra = await CadastrarEmpresa(rede, "11222333000181", true);
        await _db.SaveChangesAsync();

        var respostas = new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (url.Contains("oauth/token"))
            {
                return RespostasFixas.Json(HttpStatusCode.BadRequest, "{\"error\":{\"type\":\"invalid_grant\"}}");
            }
            return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[]}");
        });

        await CriarServico(respostas).ExecutarAsync(CancellationToken.None);

        FalhaProcessamento falha = Assert.Single(_db.Falhas);
        Assert.Equal(expirada.Id, falha.EmpresaId);
        Assert.Equal(EtapaFalha.RenovacaoToken, falha.Etapa);
        Assert.NotNull(_db.Empresas.Single(e => e.Id == outra.Id).UltimaSincronizacaoEm);
    }

    [Fact]
    public async Task Sincronizar_com_token_revogado_renova_uma_vez_e_segue()
    {
        Cliente rede = await CadastrarRede(true);
        Empresa empresa = await CadastrarEmpresa(rede, "98765432000110", true);
        int renovacoes = 0;
        var respostas = new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (url.Contains("oauth/token"))
            {
                renovacoes = renovacoes + 1;
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"access_token\":\"access-novo\",\"refresh_token\":\"refresh-novo\",\"expires_in\":21600}");
            }
            if (requisicao.Headers.Authorization?.Parameter != "access-novo")
            {
                return RespostasFixas.Json(HttpStatusCode.Unauthorized, "{\"error\":{\"type\":\"invalid_token\"}}");
            }
            return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[]}");
        });

        await CriarServico(respostas).ExecutarAsync(CancellationToken.None);

        Assert.Equal(1, renovacoes);
        Assert.NotNull(_db.Empresas.Single(e => e.Id == empresa.Id).UltimaSincronizacaoEm);
        Assert.Empty(_db.Falhas);
    }
}
