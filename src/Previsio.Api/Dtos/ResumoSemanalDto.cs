namespace Previsio.Api.Dtos;

/// <summary>
/// Configuração do resumo semanal de uma rede. Dia: 0 = domingo ... 6 = sábado; 7 = todo dia útil (segunda a sexta). Hora no horário de Brasília.
/// </summary>
public class ResumoSemanalDto
{
    public bool Ativo { get; set; }
    public int Dia { get; set; }
    public int Hora { get; set; }
    public DateTime? UltimoEnvioEm { get; set; }
    public int Destinatarios { get; set; }
}
