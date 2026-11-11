namespace Previsio.Api.Dtos;

/// <summary>
/// Configuração dos avisos de vencimento da rede.
/// AvisoVencidasDias: dias de atraso para a conta vencida virar aviso (1 = no dia seguinte ao vencimento).
/// AlertaDiario: e-mail com as contas que vencem hoje e amanhã, na hora escolhida (0 a 23, Brasília).
/// </summary>
public class AlertasVencimentoDto
{
    public int AvisoVencidasDias { get; set; }
    public bool AlertaDiarioAtivo { get; set; }
    public int AlertaDiarioHora { get; set; }
}
