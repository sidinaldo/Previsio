namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Entradas × saídas em aberto dos próximos N dias, por semana, com o saldo acumulado a partir de zero.
/// </summary>
public class ProjecaoCaixaDto
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    // A rede usa o fluxo de caixa (contas a receber); desligado, as entradas ficam zeradas
    public bool FluxoCaixaAtivo { get; set; }
    public decimal TotalEntradas { get; set; }
    public decimal TotalSaidas { get; set; }
    public decimal Saldo { get; set; }
    // Saídas em aberto já vencidas: ficam fora da projeção, mas pesam no caixa
    public decimal SaidasVencidas { get; set; }
    public decimal EntradasVencidas { get; set; }
    public decimal MediaSaidasSemana { get; set; }
    public decimal FatorSemanaCritica { get; set; }
    public int QuantidadeCriticas { get; set; }
    // Menor saldo acumulado do horizonte e a semana em que acontece
    public decimal MenorSaldoAcumulado { get; set; }
    public DateTime? SemanaMenorSaldo { get; set; }
    // Semana em que o saldo acumulado fica negativo pela primeira vez (só com o fluxo de caixa ligado)
    public DateTime? SaldoNegativoEm { get; set; }
    public List<SemanaProjecaoDto> Semanas { get; set; } = new List<SemanaProjecaoDto>();
}
