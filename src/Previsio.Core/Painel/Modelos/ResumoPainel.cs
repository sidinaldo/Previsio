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

    // Fluxo de caixa (contas a receber do Bling). Só preenchido quando a rede usa.
    public bool FluxoCaixaAtivo { get; set; }
    public decimal TotalEntradasPeriodo { get; set; }
    public int QuantidadeContasReceber { get; set; }
    // Entradas - saídas do período (as notas fora do Bling e o a receber vencido não entram)
    public decimal SaldoPeriodo { get; set; }
    // A receber vencido e não recebido: fica fora do saldo, para cobrar
    public decimal TotalReceberVencido { get; set; }
    public int QuantidadeReceberVencido { get; set; }

    public List<TotalSemana> Semanas { get; set; } = new List<TotalSemana>();
    public List<TotalEmpresa> Empresas { get; set; } = new List<TotalEmpresa>();
    // As notas pendentes mais urgentes (até CalculadoraPainel.MaximoNotasParaResolver)
    public List<NotaParaResolver> NotasParaResolver { get; set; } = new List<NotaParaResolver>();
}
