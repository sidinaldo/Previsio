namespace Previsio.Core.Painel.Modelos;

/// <summary>
/// Saídas em aberto do período de um grupo de forma de pagamento (card "Como as contas saem" do painel).
/// </summary>
public class TotalFormaPagamento
{
    // GruposFormaPagamento: boleto, pix, debito-automatico, sem...
    public string Grupo { get; set; } = "";
    public string Nome { get; set; } = "";
    // Sai sozinho da conta (débito automático); o resto alguém precisa pagar
    public bool SaiSozinho { get; set; }
    public decimal Valor { get; set; }
    public int Quantidade { get; set; }
}
