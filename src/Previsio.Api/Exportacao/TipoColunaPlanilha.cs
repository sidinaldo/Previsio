namespace Previsio.Api.Exportacao;

public enum TipoColunaPlanilha
{
    Texto = 0,
    // Número inteiro (ex.: quantidade)
    Numero = 1,
    // R$ 1.840,50
    Dinheiro = 2,
    // 18/11/2026
    Data = 3,
    // 18/11/2026 14:30
    DataHora = 4
}
