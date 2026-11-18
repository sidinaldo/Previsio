using System.Net;

namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Falha ao falar com o ERP. Leva a resposta, o método e o endereço para o registro de falhas.
/// </summary>
public class ErpException : Exception
{
    // Sem resposta do servidor (rede, DNS, tempo esgotado)
    public const HttpStatusCode SemResposta = 0;

    public HttpStatusCode StatusCode { get; }
    public string? CorpoResposta { get; }
    public string Metodo { get; }
    public string Endereco { get; }

    public ErpException(string mensagem, HttpStatusCode statusCode, string? corpoResposta)
        : this(mensagem, statusCode, corpoResposta, "", "", null)
    {
    }

    public ErpException(
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

    public bool SemPermissao
    {
        get { return StatusCode == HttpStatusCode.Forbidden; }
    }

    public bool FalhaDeConexao
    {
        get { return StatusCode == SemResposta; }
    }
}
