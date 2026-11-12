namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// PDF anexado a um e-mail que não trouxe XML.
/// </summary>
public class PdfDoEmail
{
    public string Nome { get; set; } = "";
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
}
