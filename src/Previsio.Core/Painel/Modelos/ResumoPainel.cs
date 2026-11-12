namespace Previsio.Core.Painel.Modelos;

public class ResumoPainel
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public decimal TotalPeriodo { get; set; }
    public int QuantidadeContas { get; set; }
    public decimal TotalSemanaAtual { get; set; }
    public int NotasPendentes { get; set; }
    public decimal ValorNotasPendentes { get; set; }
    // Contas em aberto no Bling com vencimento antes de hoje (últimos 30 dias sincronizados)
    public decimal TotalVencidas { get; set; }
    public int QuantidadeVencidas { get; set; }
    public List<TotalSemana> Semanas { get; set; } = new List<TotalSemana>();
    public List<TotalEmpresa> Empresas { get; set; } = new List<TotalEmpresa>();
}
