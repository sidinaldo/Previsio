namespace Previsio.Aplicacao.BuscaSefaz;

/// <summary>
/// Seção "BuscaSefaz" do appsettings: a busca completa das NF-e emitidas contra cada CNPJ.
/// Valores fora dos limites voltam para o padrão.
/// </summary>
public class BuscaSefazOptions
{
    public const string Secao = "BuscaSefaz";

    public const int IntervaloMinutosPadrao = 60;
    public const int MaximoConsultasPadrao = 10;

    // De quanto em quanto tempo cada empresa é consultada (60 a 1440). Abaixo de 60 a SEFAZ bloqueia
    // o CNPJ por 1 hora quando não há nada novo (656 - consumo indevido)
    public int IntervaloMinutos { get; set; } = IntervaloMinutosPadrao;

    // Consultas seguidas por empresa em cada execução, enquanto houver mais documentos (1 a 50; 50 documentos cada)
    public int MaximoConsultasPorExecucao { get; set; } = MaximoConsultasPadrao;

    public int IntervaloMinutosValido()
    {
        if (IntervaloMinutos < 60 || IntervaloMinutos > 1440)
        {
            return IntervaloMinutosPadrao;
        }
        return IntervaloMinutos;
    }

    public int MaximoConsultasPorExecucaoValido()
    {
        if (MaximoConsultasPorExecucao < 1 || MaximoConsultasPorExecucao > 50)
        {
            return MaximoConsultasPadrao;
        }
        return MaximoConsultasPorExecucao;
    }
}
