namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Saúde dos lançamentos no Bling: índice de conformidade (0 a 100) e os indicadores que o compõem.
/// </summary>
public class AuditoriaLancamentosDto
{
    // Nulo quando nenhum indicador tem base (rede sem contas nem notas no período)
    public int? Indice { get; set; }
    // "verde", "amarelo" ou "vermelho"
    public string Faixa { get; set; } = "";
    public int FaixaVerde { get; set; }
    public int FaixaAmarela { get; set; }
    public List<IndicadorConformidadeDto> Indicadores { get; set; } = new List<IndicadorConformidadeDto>();
    // A receber vencido e não recebido (informativo, fora do índice)
    public int QuantidadeReceberVencido { get; set; }
    public decimal ValorReceberVencido { get; set; }
}
