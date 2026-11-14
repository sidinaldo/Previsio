namespace Previsio.Core.Entidades;

/// <summary>
/// "Notas deste fornecedor vão com esta forma de pagamento", por rede. A forma é guardada pelo nome:
/// no lançamento o Previsio acha a forma de mesmo nome no Bling de cada empresa.
/// </summary>
public class RegraFormaPagamento
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // CNPJ (ou CPF) do fornecedor, só dígitos
    public string CnpjFornecedor { get; set; } = "";
    public string Forma { get; set; } = "";

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
