namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// O que interessa de um e-mail sem XML para decidir se ele trouxe uma nota em PDF.
/// </summary>
public class EmailSemXml
{
    public string MensagemId { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string Corpo { get; set; } = "";
    public string Origem { get; set; } = "";
    public List<PdfDoEmail> Pdfs { get; set; } = new List<PdfDoEmail>();
}
