using System.Net;

namespace Previsio.Bling;

/// <summary>
/// Erro ao chamar a API do Bling. Carrega o endereço chamado e a resposta,
/// para o Previsio registrar exatamente o que falhou.
/// </summary>
public class BlingException : Exception
{
    // Usado quando o Bling nem chegou a responder (sem rede, DNS, tempo esgotado)
    public const HttpStatusCode SemResposta = 0;

    public HttpStatusCode StatusCode { get; }
    public string? CorpoResposta { get; }

    // "GET", "POST"...
    public string Metodo { get; }

    // URL completa chamada, ex.: https://api.bling.com.br/Api/v3/contas/pagar
    public string Endereco { get; }

    public BlingException(string mensagem, HttpStatusCode statusCode, string? corpoResposta)
        : this(mensagem, statusCode, corpoResposta, "", "", null)
    {
    }

    public BlingException(
        string mensagem,
        HttpStatusCode statusCode,
        string? corpoResposta,
        string metodo,
        string endereco,
        Exception? erroOriginal)
        : base(mensagem, erroOriginal)
    {
        StatusCode = statusCode;
        CorpoResposta = corpoResposta;
        Metodo = metodo;
        Endereco = endereco;
    }

    public bool TokenInvalido
    {
        get { return StatusCode == HttpStatusCode.Unauthorized; }
    }

    public bool FalhaDeConexao
    {
        get { return StatusCode == SemResposta; }
    }
}
