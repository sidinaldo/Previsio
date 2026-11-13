namespace Previsio.Core.Painel.Modelos;

public class TotalSemana
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    // Saídas: contas a pagar em aberto que vencem na semana
    public decimal Valor { get; set; }
    // Entradas: contas a receber em aberto que vencem na semana (com o fluxo de caixa ligado)
    public decimal Entradas { get; set; }
    // Entradas - saídas da semana e o acumulado desde o início do período
    public decimal Saldo { get; set; }
    public decimal SaldoAcumulado { get; set; }
}
