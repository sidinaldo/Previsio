namespace Previsio.Core.NFe;

/// <summary>
/// Dados extraídos do XML de uma NF-e (modelo 55).
/// </summary>
public class NotaFiscalXml
{
    public string ChaveAcesso { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public decimal ValorTotal { get; set; }

    public string CnpjFornecedor { get; set; } = "";
    public string NomeFornecedor { get; set; } = "";
    public string CnpjDestinatario { get; set; } = "";

    public List<DuplicataXml> Duplicatas { get; set; } = new List<DuplicataXml>();
}
