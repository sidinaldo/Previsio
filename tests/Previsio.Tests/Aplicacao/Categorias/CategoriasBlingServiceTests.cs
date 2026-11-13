using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Aplicacao.Categorias;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.Categorias;

public class CategoriasBlingServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly DateTime _agora = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private Empresa _empresa = null!;

    private CategoriasBlingService CriarServico(RespostasFixas respostas)
    {
        return new CategoriasBlingService(new Repositorio<CategoriaFinanceira>(_db), BlingDeTeste.Criar(respostas),
            NullLogger<CategoriasBlingService>.Instance);
    }

    private async Task CadastrarEmpresa()
    {
        var rede = new Cliente { Nome = "Rede" };
        _db.Clientes.Add(rede);
        await _db.SaveChangesAsync();
        _empresa = new Empresa { ClienteId = rede.Id, Nome = "Loja", Cnpj = "11222333000181" };
        _db.Empresas.Add(_empresa);
        await _db.SaveChangesAsync();
    }

    private static RespostasFixas BlingComCategorias()
    {
        return new RespostasFixas(requisicao =>
        {
            string url = requisicao.RequestUri!.ToString();
            if (url.Contains("categorias/receitas-despesas"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":["
                    + "{\"id\":10,\"idCategoriaPai\":0,\"descricao\":\"Despesas operacionais\",\"tipo\":1},"
                    + "{\"id\":11,\"idCategoriaPai\":10,\"descricao\":\" Fretes \",\"tipo\":1},"
                    + "{\"id\":20,\"idCategoriaPai\":0,\"descricao\":\"Receitas de Vendas\",\"tipo\":2}]}");
            }
            // Detalhe: a conta 501 tem a categoria Fretes, as outras não têm
            if (url.Contains("contas/pagar/501"))
            {
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":{\"id\":501,\"situacao\":1,\"vencimento\":\"2026-11-20\",\"valor\":10,\"categoria\":{\"id\":11}}}");
            }
            if (url.Contains("contas/pagar/"))
            {
                string id = url.Substring(url.LastIndexOf('/') + 1);
                return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":{\"id\":" + id + ",\"situacao\":1,\"vencimento\":\"2026-11-20\",\"valor\":10,\"categoria\":{\"id\":0}}}");
            }
            return RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[]}");
        });
    }

    [Fact]
    public async Task Guarda_as_categorias_e_so_busca_de_novo_depois_de_um_dia()
    {
        await CadastrarEmpresa();
        RespostasFixas respostas = BlingComCategorias();

        await CriarServico(respostas).AtualizarSePrecisoAsync(_empresa, "conta", "token", _agora, CancellationToken.None);
        await _db.SaveChangesAsync();
        await CriarServico(respostas).AtualizarSePrecisoAsync(_empresa, "conta", "token", _agora.AddHours(5), CancellationToken.None);

        List<CategoriaFinanceira> categorias = _db.Categorias.OrderBy(c => c.BlingId).ToList();
        Assert.Equal(new long[] { 10, 11, 20 }, categorias.Select(c => c.BlingId).ToArray());
        Assert.Equal("Fretes", categorias[1].Descricao);
        Assert.Equal(10, categorias[1].BlingIdPai);
        Assert.Equal(1, respostas.Chamadas.Count(c => c.Contains("categorias")));
    }

    [Fact]
    public async Task Forcar_busca_a_lista_mesmo_atualizada_ha_pouco()
    {
        await CadastrarEmpresa();
        RespostasFixas respostas = BlingComCategorias();

        await CriarServico(respostas).AtualizarSePrecisoAsync(_empresa, "conta", "token", _agora, CancellationToken.None);
        await _db.SaveChangesAsync();
        await CriarServico(respostas).AtualizarAsync(_empresa, "conta", "token", _agora.AddMinutes(10), true, CancellationToken.None);
        await _db.SaveChangesAsync();

        Assert.Equal(2, respostas.Chamadas.Count(c => c.Contains("categorias")));
        Assert.Equal(3, _db.Categorias.Count());
    }

    [Fact]
    public async Task Sem_o_escopo_de_categorias_segue_sem_erro()
    {
        await CadastrarEmpresa();
        var respostas = new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.Forbidden, "{\"error\":{\"type\":\"insufficient_scope\"}}"));

        await CriarServico(respostas).AtualizarSePrecisoAsync(_empresa, "conta", "token", _agora, CancellationToken.None);

        Assert.Empty(_db.Categorias.Local);
    }

    private ContaPagarSincronizada Conta(long blingId, DateTime? conferidaEm, int diasVencimento)
    {
        return new ContaPagarSincronizada
        {
            EmpresaId = 1, BlingId = blingId, Vencimento = _agora.Date.AddDays(diasVencimento),
            Valor = 10m, Situacao = 1, CategoriaConferidaEm = conferidaEm, CategoriaBlingId = 99
        };
    }

    [Fact]
    public async Task Confere_primeiro_as_nunca_conferidas_e_respeita_o_maximo()
    {
        await CadastrarEmpresa();
        var contas = new List<ContaPagarSincronizada>
        {
            Conta(500, _agora.AddDays(-1), 1),   // conferida ontem: fica
            Conta(501, null, 5),                 // nunca conferida
            Conta(502, _agora.AddDays(-10), 2),  // conferida há 10 dias: revisa
            Conta(503, null, 3)                  // nunca conferida
        };
        RespostasFixas respostas = BlingComCategorias();

        int abertas = await CriarServico(respostas).ConferirCategoriasAsync(_empresa, "conta", "token", contas, 2, 7, _agora, CancellationToken.None);

        Assert.Equal(2, abertas);
        Assert.Contains(respostas.Chamadas, c => c.EndsWith("contas/pagar/503"));
        Assert.Contains(respostas.Chamadas, c => c.EndsWith("contas/pagar/501"));
        Assert.Equal(11, contas[1].CategoriaBlingId);
        Assert.Equal(_agora, contas[1].CategoriaConferidaEm);
        // Sem categoria no Bling: fica nula
        Assert.Null(contas[3].CategoriaBlingId);
        // As outras ficam para as próximas sincronizações
        Assert.Equal(_agora.AddDays(-10), contas[2].CategoriaConferidaEm);
    }

    [Fact]
    public async Task Reconfere_as_conferidas_ha_mais_dias_que_o_configurado()
    {
        await CadastrarEmpresa();
        var contas = new List<ContaPagarSincronizada>
        {
            Conta(500, _agora.AddHours(-12), 1),  // conferida há 12 horas: fica
            Conta(501, _agora.AddDays(-2), 2)     // conferida há 2 dias: com 1 dia, confere de novo
        };
        RespostasFixas respostas = BlingComCategorias();

        int abertas = await CriarServico(respostas).ConferirCategoriasAsync(_empresa, "conta", "token", contas, 50, 1, _agora, CancellationToken.None);

        Assert.Equal(1, abertas);
        Assert.Contains(respostas.Chamadas, c => c.EndsWith("contas/pagar/501"));
        Assert.DoesNotContain(respostas.Chamadas, c => c.EndsWith("contas/pagar/500"));
        Assert.Equal(_agora, contas[1].CategoriaConferidaEm);
    }

    [Fact]
    public async Task Limite_do_Bling_para_a_conferencia_sem_derrubar_a_sincronizacao()
    {
        await CadastrarEmpresa();
        var contas = new List<ContaPagarSincronizada> { Conta(501, null, 1), Conta(502, null, 2) };
        var respostas = new RespostasFixas(_ => RespostasFixas.Json((HttpStatusCode)429, "{\"error\":{\"type\":\"TOO_MANY_REQUESTS\"}}"));

        int abertas = await CriarServico(respostas).ConferirCategoriasAsync(_empresa, "conta", "token", contas, 50, 7, _agora, CancellationToken.None);

        Assert.Equal(0, abertas);
        Assert.Null(contas[0].CategoriaConferidaEm);
    }
}
