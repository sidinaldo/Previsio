namespace Previsio.Api.Sefaz;

/// <summary>
/// Documento devolvido pela distribuição da SEFAZ: o schema diz o que é
/// (procNFe = XML completo da nota; resNFe = só o resumo, antes da ciência da operação).
/// </summary>
public class DocumentoDistribuido
{
    public string Schema { get; set; } = "";
    public string Xml { get; set; } = "";

    public bool NotaCompleta
    {
        get { return Schema.StartsWith("procNFe", StringComparison.OrdinalIgnoreCase); }
    }

    public bool ResumoDaNota
    {
        get { return Schema.StartsWith("resNFe", StringComparison.OrdinalIgnoreCase); }
    }
}
