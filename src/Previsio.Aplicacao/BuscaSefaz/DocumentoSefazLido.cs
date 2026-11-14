using System.Xml;

namespace Previsio.Aplicacao.BuscaSefaz;

/// <summary>
/// O que a busca usa de um resumo de nota (resNFe) ou de um evento (procEventoNFe/resEvento).
/// </summary>
public class DocumentoSefazLido
{
    // Situação do resumo: 1 autorizada, 2 denegada, 3 cancelada
    public const string SituacaoCancelada = "3";
    public const string EventoCancelamento = "110111";

    public string Chave { get; set; } = "";
    public string Situacao { get; set; } = "";
    public string TipoEvento { get; set; } = "";

    public bool Cancelamento
    {
        get { return TipoEvento == EventoCancelamento; }
    }

    /// <summary>Nulo se o XML não abrir.</summary>
    public static DocumentoSefazLido? Ler(string xml)
    {
        var documento = new XmlDocument();
        documento.XmlResolver = null;
        try
        {
            documento.LoadXml(xml);
        }
        catch (XmlException)
        {
            return null;
        }
        var lido = new DocumentoSefazLido();
        lido.Chave = Texto(documento, "chNFe");
        lido.Situacao = Texto(documento, "cSitNFe");
        lido.TipoEvento = Texto(documento, "tpEvento");
        return lido;
    }

    private static string Texto(XmlDocument documento, string nome)
    {
        XmlNodeList nos = documento.GetElementsByTagName(nome);
        if (nos.Count == 0)
        {
            return "";
        }
        return nos[0]!.InnerText.Trim();
    }
}
