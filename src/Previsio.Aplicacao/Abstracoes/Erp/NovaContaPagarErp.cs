namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Conta a pagar a criar no ERP: uma por parcela da nota.
/// </summary>
public class NovaContaPagarErp
{
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public long ContatoId { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime Competencia { get; set; }
    public string NumeroDocumento { get; set; } = "";
    public string Historico { get; set; } = "";
    public long? CategoriaId { get; set; }
    public long? FormaPagamentoId { get; set; }
}
