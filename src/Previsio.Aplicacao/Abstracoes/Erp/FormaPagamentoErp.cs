namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Forma de pagamento do ERP. TipoPagamento usa a numeração do Bling (15 = boleto, 16 = depósito...),
/// que é a que o Core entende (GruposFormaPagamento); outro ERP converte para ela. Situacao também
/// (1 = ativa).
/// </summary>
public class FormaPagamentoErp
{
    public long Id { get; set; }
    public string Descricao { get; set; } = "";
    public int TipoPagamento { get; set; }
    public int Situacao { get; set; }
}
