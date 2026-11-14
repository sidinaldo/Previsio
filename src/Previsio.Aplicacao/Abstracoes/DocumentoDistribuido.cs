namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Documento devolvido pela distribuição da SEFAZ: o schema diz o que é
/// (procNFe = XML completo da nota; resNFe = só o resumo, antes da ciência da operação).
/// </summary>
public class DocumentoDistribuido
{
    public string Schema { get; set; } = "";
    // Número sequencial do documento na distribuição do CNPJ (15 dígitos)
    public string Nsu { get; set; } = "";
    public string Xml { get; set; } = "";

    public bool NotaCompleta
    {
        get { return Schema.StartsWith("procNFe", StringComparison.OrdinalIgnoreCase); }
    }

    public bool ResumoDaNota
    {
        get { return Schema.StartsWith("resNFe", StringComparison.OrdinalIgnoreCase); }
    }

    // Evento de uma nota (procEventoNFe completo ou resEvento resumido): cancelamento, carta de correção...
    public bool EventoDaNota
    {
        get
        {
            return Schema.StartsWith("procEventoNFe", StringComparison.OrdinalIgnoreCase)
                || Schema.StartsWith("resEvento", StringComparison.OrdinalIgnoreCase);
        }
    }
}
