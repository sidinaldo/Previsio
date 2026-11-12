namespace Previsio.Aplicacao.Comum;

/// <summary>
/// Regras dos filtros de período (de/até) das listagens.
/// </summary>
public static class ValidacaoPeriodo
{
    // Mesmo limite dos filtros de período da API do Bling
    public const int MaximoDias = 366;

    /// <summary>
    /// Mensagem de erro do período, ou null quando ele é válido.
    /// </summary>
    public static string? Validar(DateTime? de, DateTime? ate)
    {
        if (de.HasValue && ate.HasValue)
        {
            if (ate.Value.Date < de.Value.Date)
            {
                return "A data final deve ser igual ou posterior à inicial.";
            }
            if ((ate.Value.Date - de.Value.Date).TotalDays > MaximoDias)
            {
                return "O período pode ter no máximo um ano.";
            }
        }
        return null;
    }
}
