namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// GET /api/avisos: quantos são novos (o número do sininho) e os avisos abertos.
/// </summary>
public class PainelAvisosDto
{
    public int Novos { get; set; }
    public int Total { get; set; }
    public List<AvisoDto> Itens { get; set; } = new List<AvisoDto>();
}
