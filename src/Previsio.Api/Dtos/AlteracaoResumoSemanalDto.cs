namespace Previsio.Api.Dtos;

/// <summary>
/// Dia: 0 = domingo ... 6 = sábado. Hora cheia de 0 a 23, no horário de Brasília.
/// </summary>
public class AlteracaoResumoSemanalDto
{
    public bool Ativo { get; set; }
    public int Dia { get; set; }
    public int Hora { get; set; }
}
