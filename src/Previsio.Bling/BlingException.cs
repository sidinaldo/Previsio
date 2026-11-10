using System.Net;

namespace Previsio.Bling;

public class BlingException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? CorpoResposta { get; }

    public BlingException(string mensagem, HttpStatusCode statusCode, string? corpoResposta)
        : base(mensagem)
    {
        StatusCode = statusCode;
        CorpoResposta = corpoResposta;
    }

    public bool TokenInvalido
    {
        get { return StatusCode == HttpStatusCode.Unauthorized; }
    }
}
