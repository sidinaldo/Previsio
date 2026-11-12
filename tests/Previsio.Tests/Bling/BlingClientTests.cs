using System.Net;
using Previsio.Tests.Apoio;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Xunit;

namespace Previsio.Tests.Bling;

public class BlingClientTests
{
    [Fact]
    public async Task ErroDoBling_InformaMetodoEnderecoCompletoEResposta()
    {
        var respostas = new RespostasFixas(_ => RespostasFixas.Json(
            HttpStatusCode.BadRequest, "{\"error\":{\"message\":\"Id da forma de pagamento inválido.\"}}"));
        BlingClient bling = BlingDeTeste.Criar(respostas);

        BlingException erro = await Assert.ThrowsAsync<BlingException>(() =>
            bling.CriarContaPagarAsync("empresa-1", "token", new NovaContaPagarBling(), CancellationToken.None));

        Assert.Equal("POST", erro.Metodo);
        Assert.Equal(BlingDeTeste.ApiBaseUrl + "contas/pagar", erro.Endereco);
        Assert.Equal(HttpStatusCode.BadRequest, erro.StatusCode);
        Assert.Contains("forma de pagamento", erro.CorpoResposta);
        Assert.False(erro.FalhaDeConexao);
    }

    [Fact]
    public async Task SemConexao_ViraBlingExceptionComOEndereco()
    {
        var respostas = new RespostasFixas(_ => throw new HttpRequestException("Nome do servidor não resolvido"));
        BlingClient bling = BlingDeTeste.Criar(respostas);

        BlingException erro = await Assert.ThrowsAsync<BlingException>(() =>
            bling.BuscarContatoPorDocumentoAsync("empresa-1", "token", "12345678000199", CancellationToken.None));

        Assert.True(erro.FalhaDeConexao);
        Assert.Equal("GET", erro.Metodo);
        Assert.Equal(BlingDeTeste.ApiBaseUrl + "contatos?criterio=1&numeroDocumento=12345678000199", erro.Endereco);
        Assert.IsType<HttpRequestException>(erro.InnerException);
    }

    [Fact]
    public async Task RenovacaoRecusada_InformaOEnderecoDoToken()
    {
        var respostas = new RespostasFixas(_ => RespostasFixas.Json(
            HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}"));
        BlingClient bling = BlingDeTeste.Criar(respostas);

        BlingException erro = await Assert.ThrowsAsync<BlingException>(() =>
            bling.RenovarTokenAsync(bling.CredenciaisPadrao, "refresh-vencido", CancellationToken.None));

        Assert.Equal("POST", erro.Metodo);
        Assert.Equal(new BlingOptions().TokenUrl, erro.Endereco);
        Assert.Equal(HttpStatusCode.BadRequest, erro.StatusCode);
    }

    [Fact]
    public async Task TrocaDeCodigo_UsaOAplicativoInformado()
    {
        string? autorizacao = null;
        var respostas = new RespostasFixas(requisicao =>
        {
            if (requisicao.Headers.Authorization != null)
            {
                autorizacao = requisicao.Headers.Authorization.Parameter;
            }
            return RespostasFixas.Json(HttpStatusCode.OK,
                "{\"access_token\":\"a\",\"refresh_token\":\"r\",\"expires_in\":21600}");
        });
        BlingClient bling = BlingDeTeste.Criar(respostas);

        var credenciais = new CredenciaisBling();
        credenciais.ClientId = "app-da-filial";
        credenciais.ClientSecret = "segredo-da-filial";
        await bling.TrocarCodigoPorTokenAsync(credenciais, "codigo", CancellationToken.None);

        string esperado = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("app-da-filial:segredo-da-filial"));
        Assert.Equal(esperado, autorizacao);
    }

    [Fact]
    public void UrlDeAutorizacao_LevaOClientIdDoAplicativo()
    {
        BlingClient bling = BlingDeTeste.Criar(new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.OK, "{}")));

        var credenciais = new CredenciaisBling();
        credenciais.ClientId = "app-da-filial";
        credenciais.ClientSecret = "x";
        string url = bling.MontarUrlAutorizacao(credenciais, "estado");

        Assert.Contains("client_id=app-da-filial", url);
        Assert.DoesNotContain("id-teste", url);
    }
}
