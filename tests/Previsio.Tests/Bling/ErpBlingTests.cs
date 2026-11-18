using System.Net;
using Previsio.Aplicacao.Abstracoes.Erp;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Bling;

public class ErpBlingTests
{
    [Fact]
    public async Task ContaPagar_ConverteDataEReferencias()
    {
        var respostas = new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.OK,
            "{\"data\":[{\"id\":9001,\"situacao\":1,\"vencimento\":\"2026-12-05\",\"valor\":150.5,"
            + "\"contato\":{\"id\":77},\"formaPagamento\":{\"id\":31}},"
            + "{\"id\":9002,\"situacao\":2,\"vencimento\":\"05/12/2026\",\"valor\":10}]}"));
        IErp erp = BlingDeTeste.Criar(respostas);

        List<ContaPagarErp> contas = await erp.ListarContasPagarAsync(
            "empresa-1", "token", new DateTime(2026, 12, 1), new DateTime(2026, 12, 31), CancellationToken.None);

        ContaPagarErp primeira = contas.Single(c => c.Id == 9001);
        Assert.Equal(new DateTime(2026, 12, 5), primeira.Vencimento);
        Assert.Equal(77, primeira.ContatoId);
        Assert.Equal(31, primeira.FormaPagamentoId);
        Assert.Null(primeira.CategoriaId);

        // Data que não dá para ler chega nula: quem sincroniza pula a conta
        ContaPagarErp segunda = contas.Single(c => c.Id == 9002);
        Assert.Null(segunda.Vencimento);
        Assert.Null(segunda.ContatoId);
    }

    [Fact]
    public async Task NovoFornecedor_CpfVaiComoPessoaFisica()
    {
        string corpo = "";
        var respostas = new RespostasFixas(requisicao =>
        {
            corpo = requisicao.Content!.ReadAsStringAsync().Result;
            return RespostasFixas.Json(HttpStatusCode.Created, "{\"data\":{\"id\":5}}");
        });
        IErp erp = BlingDeTeste.Criar(respostas);

        var contato = new NovoContatoErp();
        contato.Nome = "Fulano";
        contato.Documento = "12345678901";
        long id = await erp.CriarContatoAsync("empresa-1", "token", contato, CancellationToken.None);

        Assert.Equal(5, id);
        Assert.Contains("\"tipo\":\"F\"", corpo);
        Assert.Contains("\"numeroDocumento\":\"12345678901\"", corpo);
    }

    [Fact]
    public async Task NovaContaPagar_MandaDatasNoFormatoDoBling()
    {
        string corpo = "";
        var respostas = new RespostasFixas(requisicao =>
        {
            corpo = requisicao.Content!.ReadAsStringAsync().Result;
            return RespostasFixas.Json(HttpStatusCode.Created, "{\"data\":{\"id\":123}}");
        });
        IErp erp = BlingDeTeste.Criar(respostas);

        var conta = new NovaContaPagarErp();
        conta.Vencimento = new DateTime(2026, 12, 5);
        conta.DataEmissao = new DateTime(2026, 11, 20);
        conta.Competencia = new DateTime(2026, 11, 20);
        conta.Valor = 300;
        conta.ContatoId = 77;
        conta.CategoriaId = 12;
        await erp.CriarContaPagarAsync("empresa-1", "token", conta, CancellationToken.None);

        Assert.Contains("\"vencimento\":\"2026-12-05\"", corpo);
        Assert.Contains("\"dataEmissao\":\"2026-11-20\"", corpo);
        Assert.Contains("\"contato\":{\"id\":77}", corpo);
        Assert.Contains("\"categoria\":{\"id\":12}", corpo);
        Assert.DoesNotContain("formaPagamento", corpo);
    }

    [Fact]
    public async Task ErroDoBling_ChegaComoErpException()
    {
        var respostas = new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.Unauthorized, "{\"error\":{}}"));
        IErp erp = BlingDeTeste.Criar(respostas);

        ErpException erro = await Assert.ThrowsAnyAsync<ErpException>(() =>
            erp.ListarCategoriasAsync("empresa-1", "token", CancellationToken.None));

        Assert.True(erro.TokenInvalido);
    }
}
