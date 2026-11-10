namespace Previsio.Core.Entidades;

public class ParcelaNota
{
    public int Id { get; set; }
    public int NotaFiscalId { get; set; }
    public NotaFiscal? NotaFiscal { get; set; }

    public string NumeroParcela { get; set; } = "";
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }

    // Preenchido quando a conta a pagar é criada no Bling
    public long? BlingContaPagarId { get; set; }
}
