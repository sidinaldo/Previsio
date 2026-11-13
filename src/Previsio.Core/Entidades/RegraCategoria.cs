namespace Previsio.Core.Entidades;

/// <summary>
/// "Notas deste fornecedor vão para esta categoria", por rede. A categoria é guardada pelo nome:
/// no lançamento o Previsio acha a categoria de mesmo nome no Bling de cada empresa.
/// </summary>
public class RegraCategoria
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // CNPJ (ou CPF) do fornecedor, só dígitos
    public string CnpjFornecedor { get; set; } = "";
    public string Categoria { get; set; } = "";

    public DateTime CriadaEm { get; set; } = DateTime.UtcNow;
}
