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
