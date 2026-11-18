using System.Net;
using Previsio.Aplicacao.Abstracoes.Erp;

namespace Previsio.Bling;

/// <summary>
/// Erro ao chamar a API do Bling. Carrega o endereço chamado e a resposta,
/// para o Previsio registrar exatamente o que falhou.
/// </summary>
public class BlingException : ErpException
{
    public BlingException(string mensagem, HttpStatusCode statusCode, string? corpoResposta)
        : base(mensagem, statusCode, corpoResposta)
    {
    }

    public BlingException(
        string mensagem,
        HttpStatusCode statusCode,
        string? corpoResposta,
        string metodo,
        string endereco,
        Exception? erroOriginal)
        : base(mensagem, statusCode, corpoResposta, metodo, endereco, erroOriginal)
    {
    }
}
