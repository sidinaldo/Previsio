namespace Previsio.Api.Servicos;

/// <summary>
/// "Hoje" no horário de Brasília, para que o painel não vire o dia às 21h.
/// </summary>
public static class RelogioBrasil
{
    public static readonly TimeZoneInfo Fuso = CarregarFuso();

    public static DateTime Hoje()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso).Date;
    }

    /// <summary>
    /// Instante UTC em que começa o dia informado no horário de Brasília (filtros de período: no banco tudo é UTC).
    /// </summary>
    public static DateTime InicioDoDiaEmUtc(DateTime dia)
    {
        DateTime inicioLocal = DateTime.SpecifyKind(dia.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Fuso);
    }

    private static TimeZoneInfo CarregarFuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
