namespace Previsio.Api.Sincronizacao;

/// <summary>
/// Seção "Sincronizacao" do appsettings: janela e frequência da cópia das contas a pagar do Bling.
/// Vale para todas as redes, porque mexe no número de chamadas ao Bling.
/// Os valores fora dos limites voltam para o padrão.
/// </summary>
public class SincronizacaoOptions
{
    public const string Secao = "Sincronizacao";

    public const int DiasPassadosPadrao = 30;
    public const int DiasFuturosPadrao = 90;
    public const int IntervaloMinutosPadrao = 30;
    public const int DiasRenovarFornecedorPadrao = 30;
    public const int MaximoContasAntigasPadrao = 100;

    // Vencimentos de quantos dias atrás entram (as vencidas do painel e dos avisos saem daqui)
    public int DiasPassados { get; set; } = DiasPassadosPadrao;

    // Vencimentos até quantos dias à frente entram
    public int DiasFuturos { get; set; } = DiasFuturosPadrao;

    // De quantos em quantos minutos a sincronização roda (5 a 1440)
    public int IntervaloMinutos { get; set; } = IntervaloMinutosPadrao;

    // Depois de quantos dias o nome guardado de um fornecedor é buscado de novo no Bling
    public int DiasRenovarFornecedor { get; set; } = DiasRenovarFornecedorPadrao;

    // Contas em aberto vencidas antes da janela conferidas uma a uma, por empresa, a cada sincronização (1 a 1000)
    public int MaximoContasAntigas { get; set; } = MaximoContasAntigasPadrao;

    public int DiasPassadosValidos()
    {
        return DentroDoLimite(DiasPassados, 0, 365, DiasPassadosPadrao);
    }

    public int DiasFuturosValidos()
    {
        return DentroDoLimite(DiasFuturos, 1, 365, DiasFuturosPadrao);
    }

    public int IntervaloMinutosValido()
    {
        return DentroDoLimite(IntervaloMinutos, 5, 1440, IntervaloMinutosPadrao);
    }

    public int DiasRenovarFornecedorValidos()
    {
        return DentroDoLimite(DiasRenovarFornecedor, 1, 365, DiasRenovarFornecedorPadrao);
    }

    public int MaximoContasAntigasValido()
    {
        return DentroDoLimite(MaximoContasAntigas, 1, 1000, MaximoContasAntigasPadrao);
    }

    /// <summary>
    /// Agenda do Hangfire para o intervalo. Até 59 minutos: "a cada N minutos";
    /// acima disso, arredonda para horas inteiras.
    /// </summary>
    public string Cron()
    {
        int minutos = IntervaloMinutosValido();
        if (minutos < 60)
        {
            return "*/" + minutos + " * * * *";
        }
        int horas = minutos / 60;
        if (horas >= 24)
        {
            return "0 0 * * *";
        }
        if (horas == 1)
        {
            return "0 * * * *";
        }
        return "0 */" + horas + " * * *";
    }

    private static int DentroDoLimite(int valor, int minimo, int maximo, int padrao)
    {
        if (valor < minimo || valor > maximo)
        {
            return padrao;
        }
        return valor;
    }
}
