namespace Previsio.Api.Dtos;

/// <summary>
/// GET /api/jobs/{nome}/situacao: true enquanto o job está na fila ou rodando.
/// </summary>
public class SituacaoJobDto
{
    public string Nome { get; set; } = "";
    public bool EmExecucao { get; set; }
}
