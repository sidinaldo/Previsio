using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Previsio.Core.NFe;

/// <summary>
/// Lê o XML de uma NF-e. Aceita tanto o arquivo autorizado (nfeProc)
/// quanto o XML só com a tag NFe. Qualquer outro XML retorna null.
/// </summary>
public class LeitorNFe
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public NotaFiscalXml? Ler(string conteudoXml)
    {
        if (string.IsNullOrWhiteSpace(conteudoXml))
        {
            return null;
        }

        XDocument documento;
        try
        {
            documento = XDocument.Parse(conteudoXml);
        }
        catch (XmlException)
        {
            return null;
        }

        XElement? infNFe = PrimeiroElemento(documento.Root, "infNFe");
        if (infNFe == null)
        {
            // Pode ser evento de cancelamento, CT-e, NFS-e etc. Não é NF-e.
            return null;
        }

        XElement? ide = Filho(infNFe, "ide");
        XElement? emit = Filho(infNFe, "emit");
        XElement? dest = Filho(infNFe, "dest");
        if (ide == null || emit == null || dest == null)
        {
            return null;
        }

        string modelo = TextoFilho(ide, "mod");
        if (modelo != "" && modelo != "55")
        {
            // NFC-e (65) não entra no contas a pagar
            return null;
        }

        var nota = new NotaFiscalXml();
        nota.ChaveAcesso = ObterChave(documento.Root, infNFe);
        nota.Numero = TextoFilho(ide, "nNF");
        nota.Serie = TextoFilho(ide, "serie");
        nota.DataEmissao = LerDataEmissao(ide);

        nota.CnpjFornecedor = SomenteDigitos(TextoFilho(emit, "CNPJ"));
        if (nota.CnpjFornecedor == "")
        {
            nota.CnpjFornecedor = SomenteDigitos(TextoFilho(emit, "CPF"));
        }
        nota.NomeFornecedor = TextoFilho(emit, "xNome");

        nota.CnpjDestinatario = SomenteDigitos(TextoFilho(dest, "CNPJ"));

        XElement? icmsTot = PrimeiroElemento(infNFe, "ICMSTot");
        if (icmsTot != null)
        {
            nota.ValorTotal = LerDecimal(TextoFilho(icmsTot, "vNF"));
        }

        XElement? cobr = Filho(infNFe, "cobr");
        if (cobr != null)
        {
            foreach (XElement dup in cobr.Elements().Where(e => e.Name.LocalName == "dup"))
            {
                var duplicata = new DuplicataXml();
                duplicata.Numero = TextoFilho(dup, "nDup");
                duplicata.Vencimento = LerData(TextoFilho(dup, "dVenc"));
                duplicata.Valor = LerDecimal(TextoFilho(dup, "vDup"));

                if (duplicata.Valor > 0 && duplicata.Vencimento != DateTime.MinValue)
                {
                    nota.Duplicatas.Add(duplicata);
                }
            }
        }

        if (nota.ChaveAcesso.Length != 44 || nota.CnpjDestinatario == "")
        {
            return null;
        }

        return nota;
    }

    private static string ObterChave(XElement? raiz, XElement infNFe)
    {
        // Preferência: chave do protocolo de autorização
        XElement? chNFe = PrimeiroElemento(raiz, "chNFe");
        if (chNFe != null)
        {
            string chave = SomenteDigitos(chNFe.Value);
            if (chave.Length == 44)
            {
                return chave;
            }
        }

        // Alternativa: atributo Id="NFe3519..."
        XAttribute? id = infNFe.Attribute("Id");
        if (id != null)
        {
            return SomenteDigitos(id.Value);
        }

        return "";
    }

    private static DateTime LerDataEmissao(XElement ide)
    {
        string dhEmi = TextoFilho(ide, "dhEmi");
        if (dhEmi != "")
        {
            DateTimeOffset dataComFuso;
            if (DateTimeOffset.TryParse(dhEmi, Invariante, DateTimeStyles.None, out dataComFuso))
            {
                return dataComFuso.Date;
            }
        }

        // Layout antigo (versão 2.00) usava dEmi
        return LerData(TextoFilho(ide, "dEmi"));
    }

    private static DateTime LerData(string texto)
    {
        DateTime data;
        if (DateTime.TryParseExact(texto, "yyyy-MM-dd", Invariante, DateTimeStyles.None, out data))
        {
            return data;
        }
        return DateTime.MinValue;
    }

    private static decimal LerDecimal(string texto)
    {
        decimal valor;
        if (decimal.TryParse(texto, NumberStyles.Number, Invariante, out valor))
        {
            return valor;
        }
        return 0m;
    }

    private static XElement? PrimeiroElemento(XElement? raiz, string nome)
    {
        if (raiz == null)
        {
            return null;
        }
        if (raiz.Name.LocalName == nome)
        {
            return raiz;
        }
        return raiz.Descendants().FirstOrDefault(e => e.Name.LocalName == nome);
    }

    private static XElement? Filho(XElement pai, string nome)
    {
        return pai.Elements().FirstOrDefault(e => e.Name.LocalName == nome);
    }

    private static string TextoFilho(XElement pai, string nome)
    {
        XElement? elemento = Filho(pai, nome);
        if (elemento == null)
        {
            return "";
        }
        return elemento.Value.Trim();
    }

    public static string SomenteDigitos(string texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return "";
        }
        return new string(texto.Where(char.IsDigit).ToArray());
    }
}
