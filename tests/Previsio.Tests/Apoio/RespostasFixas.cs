using System.Net;
using System.Text;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Faz o papel do Bling nos testes: responde cada chamada com o que o teste definir
/// e guarda as requisições recebidas.
/// </summary>
public class RespostasFixas : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<string> Chamadas { get; } = new List<string>();

    public RespostasFixas(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json)
    {
        var resposta = new HttpResponseMessage(status);
        resposta.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return resposta;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage requisicao, CancellationToken cancelamento)
    {
        string endereco = "";
        if (requisicao.RequestUri != null)
        {
            endereco = requisicao.RequestUri.ToString();
        }
        Chamadas.Add(requisicao.Method.Method + " " + endereco);

        return Task.FromResult(_responder(requisicao));
    }
}
