using MailKit;

namespace Previsio.Api.Jobs;

/// <summary>
/// Regras para saber quais e-mails ainda não foram lidos pelo Previsio,
/// sem depender de "lido/não lido" (uma pessoa pode abrir o e-mail antes do job).
/// O IMAP numera cada e-mail da caixa com um UID crescente; guardamos o último lido.
/// </summary>
public static class ControleUidEmail
{
    // Na primeira leitura (ou se o servidor renumerar a caixa), lê os e-mails desses últimos dias
    public const int DiasPrimeiraLeitura = 30;

    // Depois de falhar esse número de vezes, o e-mail é pulado para não travar os seguintes
    public const int MaximoTentativasPorEmail = 3;

    /// <summary>
    /// True quando não há ponto de partida confiável: nunca leu a caixa,
    /// ou o UIDVALIDITY mudou (o servidor renumerou os e-mails e os UIDs antigos não valem mais).
    /// </summary>
    public static bool PrecisaRecomecar(long? validadeGravada, long? ultimoUid, long validadeAtual)
    {
        if (!validadeGravada.HasValue || !ultimoUid.HasValue)
        {
            return true;
        }
        if (validadeGravada.Value != validadeAtual)
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// UIDs posteriores ao último lido, do mais antigo para o mais novo, limitados ao máximo por execução.
    /// A busca "UID n:*" do IMAP devolve o último e-mail mesmo quando ele é anterior a n,
    /// por isso o filtro aqui.
    /// </summary>
    public static List<UniqueId> ProximosParaLer(IList<UniqueId> encontrados, long? ultimoUid, int maximo)
    {
        var resultado = new List<UniqueId>();
        foreach (UniqueId uid in encontrados.OrderBy(u => u.Id))
        {
            if (ultimoUid.HasValue && uid.Id <= ultimoUid.Value)
            {
                continue;
            }
            resultado.Add(uid);
            if (resultado.Count >= maximo)
            {
                break;
            }
        }
        return resultado;
    }
}
