namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Parcela (duplicata) da nota. ErpContaPagarId vem preenchido depois que a conta a pagar foi criada no Bling.
/// </summary>
public class ParcelaNotaDto
{
    public string NumeroParcela { get; set; } = "";
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public long? ErpContaPagarId { get; set; }
}
