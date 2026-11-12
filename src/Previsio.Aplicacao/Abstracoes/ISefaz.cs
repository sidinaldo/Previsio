using System.Security.Cryptography.X509Certificates;

namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Conversa com o Ambiente Nacional da NF-e usando o certificado A1 da empresa.
/// </summary>
public interface ISefaz
{
    /// <summary>
    /// Procura a nota pela chave como interessada (destinatária) o CNPJ informado.
    /// </summary>
    Task<RetornoSefaz> ConsultarPorChaveAsync(
        X509Certificate2 certificado, string uf, string cnpj, string chave, CancellationToken cancelamento);

    /// <summary>
    /// Registra a Ciência da Operação da nota, que libera o download do XML completo.
    /// </summary>
    Task<RetornoSefaz> ManifestarCienciaAsync(
        X509Certificate2 certificado, string cnpj, string chave, CancellationToken cancelamento);
}
