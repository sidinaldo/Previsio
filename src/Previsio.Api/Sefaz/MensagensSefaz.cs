using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace Previsio.Api.Sefaz;

/// <summary>
/// Monta e lê as mensagens SOAP do Ambiente Nacional da NF-e:
/// consulta da nota pela chave (NFeDistribuicaoDFe, consChNFe) e evento de Ciência da Operação (210210).
/// </summary>
public static class MensagensSefaz
{
    public const string NamespaceNfe = "http://www.portalfiscal.inf.br/nfe";
    public const string AcaoDistribuicao = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe/nfeDistDFeInteresse";
    public const string AcaoEvento = "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4/nfeRecepcaoEvento";
    public const string EventoCiencia = "210210";

    private const string Soap = "http://www.w3.org/2003/05/soap-envelope";

    public static string ConsultaPorChave(string tipoAmbiente, string codigoUf, string cnpj, string chave)
    {
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<soap12:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap12=\"" + Soap + "\">"
            + "<soap12:Body>"
            + "<nfeDistDFeInteresse xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe\">"
            + "<nfeDadosMsg>"
            + "<distDFeInt versao=\"1.01\" xmlns=\"" + NamespaceNfe + "\">"
            + "<tpAmb>" + tipoAmbiente + "</tpAmb>"
            + "<cUFAutor>" + codigoUf + "</cUFAutor>"
            + "<CNPJ>" + cnpj + "</CNPJ>"
            + "<consChNFe><chNFe>" + chave + "</chNFe></consChNFe>"
            + "</distDFeInt>"
            + "</nfeDadosMsg>"
            + "</nfeDistDFeInteresse>"
            + "</soap12:Body>"
            + "</soap12:Envelope>";
    }

    /// <summary>
    /// Evento de Ciência da Operação assinado com o certificado da empresa, já no envelope SOAP.
    /// </summary>
    public static string EventoCienciaAssinado(
        string tipoAmbiente, string cnpj, string chave, DateTimeOffset quando, X509Certificate2 certificado)
    {
        string id = "ID" + EventoCiencia + chave + "01";
        string evento =
            "<evento versao=\"1.00\" xmlns=\"" + NamespaceNfe + "\">"
            + "<infEvento Id=\"" + id + "\">"
            + "<cOrgao>91</cOrgao>"
            + "<tpAmb>" + tipoAmbiente + "</tpAmb>"
            + "<CNPJ>" + cnpj + "</CNPJ>"
            + "<chNFe>" + chave + "</chNFe>"
            + "<dhEvento>" + quando.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture) + "</dhEvento>"
            + "<tpEvento>" + EventoCiencia + "</tpEvento>"
            + "<nSeqEvento>1</nSeqEvento>"
            + "<verEvento>1.00</verEvento>"
            + "<detEvento versao=\"1.00\"><descEvento>Ciencia da Operacao</descEvento></detEvento>"
            + "</infEvento>"
            + "</evento>";

        string eventoAssinado = Assinar(evento, id, certificado);
        string lote = DateTime.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);

        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
            + "<soap12:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap12=\"" + Soap + "\">"
            + "<soap12:Body>"
            + "<nfeDadosMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4\">"
            + "<envEvento versao=\"1.00\" xmlns=\"" + NamespaceNfe + "\">"
            + "<idLote>" + lote + "</idLote>"
            + eventoAssinado
            + "</envEvento>"
            + "</nfeDadosMsg>"
            + "</soap12:Body>"
            + "</soap12:Envelope>";
    }

    /// <summary>
    /// Assinatura XML (enveloped, C14N, RSA-SHA1 e SHA1, como exige o manual da NF-e) do elemento com o Id.
    /// </summary>
    public static string Assinar(string xml, string id, X509Certificate2 certificado)
    {
        var documento = new XmlDocument();
        documento.PreserveWhitespace = true;
        documento.LoadXml(xml);

        var assinatura = new SignedXml(documento);
        assinatura.SigningKey = certificado.GetRSAPrivateKey();
        assinatura.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigC14NTransformUrl;
        assinatura.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;

        var referencia = new Reference("#" + id);
        referencia.DigestMethod = SignedXml.XmlDsigSHA1Url;
        referencia.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        referencia.AddTransform(new XmlDsigC14NTransform());
        assinatura.AddReference(referencia);

        var chave = new KeyInfo();
        chave.AddClause(new KeyInfoX509Data(certificado));
        assinatura.KeyInfo = chave;

        assinatura.ComputeSignature();
        XmlElement elementoAssinatura = assinatura.GetXml();
        documento.DocumentElement!.AppendChild(documento.ImportNode(elementoAssinatura, true));
        return documento.DocumentElement.OuterXml;
    }

    /// <summary>
    /// Retorno da distribuição (retDistDFeInt): status, motivo e os documentos descompactados.
    /// </summary>
    public static RetornoSefaz LerRetornoDistribuicao(string respostaSoap)
    {
        var retorno = new RetornoSefaz();
        XmlDocument documento = Carregar(respostaSoap);
        XmlElement? ret = Primeiro(documento, "retDistDFeInt");
        if (ret == null)
        {
            retorno.Status = "?";
            retorno.Motivo = "A SEFAZ respondeu sem o retorno da distribuição.";
            return retorno;
        }
        retorno.Status = Texto(ret, "cStat");
        retorno.Motivo = Texto(ret, "xMotivo");

        foreach (XmlNode no in ret.GetElementsByTagName("docZip", NamespaceNfe))
        {
            XmlElement? doc = no as XmlElement;
            if (doc == null)
            {
                continue;
            }
            var distribuido = new DocumentoDistribuido();
            distribuido.Schema = doc.GetAttribute("schema");
            distribuido.Xml = Descompactar(doc.InnerText.Trim());
            retorno.Documentos.Add(distribuido);
        }
        return retorno;
    }

    /// <summary>
    /// Retorno do evento: o status do evento (retEvento) quando houver; senão, o do lote.
    /// </summary>
    public static RetornoSefaz LerRetornoEvento(string respostaSoap)
    {
        var retorno = new RetornoSefaz();
        XmlDocument documento = Carregar(respostaSoap);
        XmlElement? infEvento = null;
        XmlElement? retEvento = Primeiro(documento, "retEvento");
        if (retEvento != null)
        {
            infEvento = Primeiro(retEvento, "infEvento");
        }
        if (infEvento != null)
        {
            retorno.Status = Texto(infEvento, "cStat");
            retorno.Motivo = Texto(infEvento, "xMotivo");
            return retorno;
        }
        XmlElement? lote = Primeiro(documento, "retEnvEvento");
        if (lote != null)
        {
            retorno.Status = Texto(lote, "cStat");
            retorno.Motivo = Texto(lote, "xMotivo");
            return retorno;
        }
        retorno.Status = "?";
        retorno.Motivo = "A SEFAZ respondeu sem o retorno do evento.";
        return retorno;
    }

    public static string Descompactar(string base64)
    {
        byte[] compactado = Convert.FromBase64String(base64);
        using (var entrada = new MemoryStream(compactado))
        using (var gzip = new GZipStream(entrada, CompressionMode.Decompress))
        using (var leitor = new StreamReader(gzip, Encoding.UTF8))
        {
            return leitor.ReadToEnd();
        }
    }

    private static XmlDocument Carregar(string xml)
    {
        var documento = new XmlDocument();
        documento.XmlResolver = null;
        documento.LoadXml(xml);
        return documento;
    }

    private static XmlElement? Primeiro(XmlNode raiz, string nome)
    {
        XmlNodeList? lista = null;
        XmlDocument? documento = raiz as XmlDocument;
        if (documento != null)
        {
            lista = documento.GetElementsByTagName(nome, NamespaceNfe);
        }
        else
        {
            XmlElement? elemento = raiz as XmlElement;
            if (elemento != null)
            {
                lista = elemento.GetElementsByTagName(nome, NamespaceNfe);
            }
        }
        if (lista == null || lista.Count == 0)
        {
            return null;
        }
        return lista[0] as XmlElement;
    }

    private static string Texto(XmlElement pai, string nome)
    {
        XmlElement? filho = Primeiro(pai, nome);
        if (filho == null)
        {
            return "";
        }
        return filho.InnerText.Trim();
    }
}
