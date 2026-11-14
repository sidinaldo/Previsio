using System.Xml;

namespace Previsio.SondagemSefaz;

/// <summary>
/// O que interessa de cada documento da distribuição, para o relatório da sondagem.
/// </summary>
public class ResumoDocumento
{
    public string Nsu { get; set; } = "";
    public string Schema { get; set; } = "";
    public string Chave { get; set; } = "";
    public string Emitente { get; set; } = "";
    public string Valor { get; set; } = "";
    public string Emissao { get; set; } = "";
    public string Detalhe { get; set; } = "";

    public static ResumoDocumento Ler(string nsu, string schema, string xml)
    {
        var resumo = new ResumoDocumento();
        resumo.Nsu = nsu;
        resumo.Schema = schema;
        var documento = new XmlDocument();
        documento.XmlResolver = null;
        try
        {
            documento.LoadXml(xml);
        }
        catch (XmlException)
        {
            resumo.Detalhe = "XML não abriu";
            return resumo;
        }
        resumo.Chave = Primeiro(documento, "chNFe");
        resumo.Emitente = Primeiro(documento, "xNome");
        resumo.Valor = Primeiro(documento, "vNF");
        resumo.Emissao = Primeiro(documento, "dhEmi");
        if (schema.StartsWith("resNFe", StringComparison.OrdinalIgnoreCase))
        {
            // cSitNFe: 1 = autorizada, 2 = denegada, 3 = cancelada
            resumo.Detalhe = "situação " + Primeiro(documento, "cSitNFe") + "; manifestação " + Primeiro(documento, "cSitConf");
        }
        else if (schema.StartsWith("procEventoNFe", StringComparison.OrdinalIgnoreCase)
            || schema.StartsWith("resEvento", StringComparison.OrdinalIgnoreCase))
        {
            resumo.Detalhe = "evento " + Primeiro(documento, "tpEvento") + " " + Primeiro(documento, "xEvento");
        }
        else if (schema.StartsWith("procNFe", StringComparison.OrdinalIgnoreCase))
        {
            int parcelas = documento.GetElementsByTagName("dup").Count;
            resumo.Detalhe = parcelas + " parcela(s) no XML";
        }
        return resumo;
    }

    private static string Primeiro(XmlDocument documento, string nome)
    {
        XmlNodeList nos = documento.GetElementsByTagName(nome);
        if (nos.Count == 0)
        {
            return "";
        }
        return nos[0]!.InnerText.Trim();
    }
}
