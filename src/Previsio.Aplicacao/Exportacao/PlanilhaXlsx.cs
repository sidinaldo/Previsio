using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Previsio.Aplicacao.Exportacao;

/// <summary>
/// Gera um arquivo Excel (.xlsx) de uma aba, sem biblioteca externa: o .xlsx é um zip de XMLs (Office Open XML).
/// Cabeçalho em negrito com filtro e primeira linha congelada; valores em reais, datas e datas com hora
/// já no formato brasileiro, como números de verdade (dá para somar e filtrar no Excel).
/// </summary>
public class PlanilhaXlsx
{
    // Índices dos estilos em styles.xml (cellXfs)
    private const int EstiloCabecalho = 1;
    private const int EstiloDinheiro = 2;
    private const int EstiloData = 3;
    private const int EstiloDataHora = 4;

    private static readonly DateTime InicioDatasExcel = new DateTime(1899, 12, 30);

    private readonly string _nomeAba;
    private readonly List<ColunaPlanilha> _colunas;
    private readonly List<object?[]> _linhas = new List<object?[]>();

    public PlanilhaXlsx(string nomeAba, List<ColunaPlanilha> colunas)
    {
        _nomeAba = NomeAbaValido(nomeAba);
        _colunas = colunas;
    }

    public int QuantidadeLinhas
    {
        get { return _linhas.Count; }
    }

    /// <summary>
    /// Uma linha, na ordem das colunas. Nulo deixa a célula vazia.
    /// Texto: string. Número e dinheiro: int, long ou decimal. Data e data com hora: DateTime (já no horário de Brasília).
    /// </summary>
    public void AdicionarLinha(params object?[] valores)
    {
        if (valores.Length != _colunas.Count)
        {
            throw new ArgumentException("A linha tem " + valores.Length + " valores e a planilha tem " + _colunas.Count + " colunas.");
        }
        _linhas.Add(valores);
    }

    public byte[] Gerar()
    {
        using (var memoria = new MemoryStream())
        {
            using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
            {
                Gravar(zip, "[Content_Types].xml", TiposDeConteudo());
                Gravar(zip, "_rels/.rels", RelacoesRaiz());
                Gravar(zip, "xl/workbook.xml", Pasta());
                Gravar(zip, "xl/_rels/workbook.xml.rels", RelacoesPasta());
                Gravar(zip, "xl/styles.xml", Estilos());
                GravarAba(zip);
            }
            return memoria.ToArray();
        }
    }

    private static void Gravar(ZipArchive zip, string caminho, string conteudo)
    {
        ZipArchiveEntry entrada = zip.CreateEntry(caminho, CompressionLevel.Optimal);
        using (var escritor = new StreamWriter(entrada.Open(), new UTF8Encoding(false)))
        {
            escritor.Write(conteudo);
        }
    }

    private void GravarAba(ZipArchive zip)
    {
        ZipArchiveEntry entrada = zip.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Optimal);
        var configuracao = new XmlWriterSettings();
        configuracao.Encoding = new UTF8Encoding(false);
        using (Stream destino = entrada.Open())
        using (XmlWriter xml = XmlWriter.Create(destino, configuracao))
        {
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            xml.WriteStartDocument(true);
            xml.WriteStartElement("worksheet", ns);

            // Primeira linha congelada: o cabeçalho fica visível ao rolar
            xml.WriteStartElement("sheetViews", ns);
            xml.WriteStartElement("sheetView", ns);
            xml.WriteAttributeString("workbookViewId", "0");
            xml.WriteStartElement("pane", ns);
            xml.WriteAttributeString("ySplit", "1");
            xml.WriteAttributeString("topLeftCell", "A2");
            xml.WriteAttributeString("activePane", "bottomLeft");
            xml.WriteAttributeString("state", "frozen");
            xml.WriteEndElement();
            xml.WriteEndElement();
            xml.WriteEndElement();

            xml.WriteStartElement("cols", ns);
            for (int i = 0; i < _colunas.Count; i++)
            {
                xml.WriteStartElement("col", ns);
                xml.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("width", _colunas[i].Largura.ToString(CultureInfo.InvariantCulture));
                xml.WriteAttributeString("customWidth", "1");
                xml.WriteEndElement();
            }
            xml.WriteEndElement();

            xml.WriteStartElement("sheetData", ns);
            xml.WriteStartElement("row", ns);
            xml.WriteAttributeString("r", "1");
            for (int i = 0; i < _colunas.Count; i++)
            {
                CelulaTexto(xml, ns, Referencia(i, 1), _colunas[i].Titulo, EstiloCabecalho);
            }
            xml.WriteEndElement();

            int numeroLinha = 2;
            foreach (object?[] linha in _linhas)
            {
                xml.WriteStartElement("row", ns);
                xml.WriteAttributeString("r", numeroLinha.ToString(CultureInfo.InvariantCulture));
                for (int i = 0; i < _colunas.Count; i++)
                {
                    Celula(xml, ns, Referencia(i, numeroLinha), _colunas[i].Tipo, linha[i]);
                }
                xml.WriteEndElement();
                numeroLinha++;
            }
            xml.WriteEndElement();

            xml.WriteStartElement("autoFilter", ns);
            xml.WriteAttributeString("ref", "A1:" + Referencia(_colunas.Count - 1, Math.Max(1, _linhas.Count + 1)));
            xml.WriteEndElement();

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }
    }

    private static void Celula(XmlWriter xml, string ns, string referencia, TipoColunaPlanilha tipo, object? valor)
    {
        if (valor == null)
        {
            return;
        }

        if (tipo == TipoColunaPlanilha.Texto)
        {
            CelulaTexto(xml, ns, referencia, Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "", 0);
            return;
        }

        string numero;
        int estilo = 0;
        if (tipo == TipoColunaPlanilha.Data || tipo == TipoColunaPlanilha.DataHora)
        {
            DateTime data = (DateTime)valor;
            double serial;
            if (tipo == TipoColunaPlanilha.Data)
            {
                serial = (data.Date - InicioDatasExcel).TotalDays;
                estilo = EstiloData;
            }
            else
            {
                // Segundos inteiros e precisão total: com menos casas, 11:00 aparece como 10:59 no Excel
                long segundos = (long)Math.Round((data - InicioDatasExcel).TotalSeconds);
                serial = segundos / 86400.0;
                estilo = EstiloDataHora;
            }
            numero = serial.ToString("R", CultureInfo.InvariantCulture);
        }
        else
        {
            numero = Convert.ToDecimal(valor, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (tipo == TipoColunaPlanilha.Dinheiro)
            {
                estilo = EstiloDinheiro;
            }
        }

        xml.WriteStartElement("c", ns);
        xml.WriteAttributeString("r", referencia);
        if (estilo > 0)
        {
            xml.WriteAttributeString("s", estilo.ToString(CultureInfo.InvariantCulture));
        }
        xml.WriteElementString("v", ns, numero);
        xml.WriteEndElement();
    }

    private static void CelulaTexto(XmlWriter xml, string ns, string referencia, string texto, int estilo)
    {
        xml.WriteStartElement("c", ns);
        xml.WriteAttributeString("r", referencia);
        xml.WriteAttributeString("t", "inlineStr");
        if (estilo > 0)
        {
            xml.WriteAttributeString("s", estilo.ToString(CultureInfo.InvariantCulture));
        }
        xml.WriteStartElement("is", ns);
        xml.WriteStartElement("t", ns);
        xml.WriteAttributeString("xml", "space", null, "preserve");
        xml.WriteString(TextoValido(texto));
        xml.WriteEndElement();
        xml.WriteEndElement();
        xml.WriteEndElement();
    }

    // Caracteres de controle (vindos de XML ou mensagens de erro) não são aceitos no XML da planilha
    private static string TextoValido(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (char caractere in texto)
        {
            if (XmlConvert.IsXmlChar(caractere))
            {
                resultado.Append(caractere);
            }
        }
        // Limite de uma célula do Excel
        if (resultado.Length > 32767)
        {
            return resultado.ToString(0, 32767);
        }
        return resultado.ToString();
    }

    /// <summary>
    /// "A1", "B7", "AA10"... a partir da coluna (base 0) e da linha (base 1).
    /// </summary>
    public static string Referencia(int coluna, int linha)
    {
        string letras = "";
        int numero = coluna + 1;
        while (numero > 0)
        {
            int resto = (numero - 1) % 26;
            letras = (char)('A' + resto) + letras;
            numero = (numero - 1) / 26;
        }
        return letras + linha.ToString(CultureInfo.InvariantCulture);
    }

    // O Excel não aceita alguns caracteres no nome da aba, nem mais de 31 caracteres
    private static string NomeAbaValido(string nome)
    {
        var resultado = new StringBuilder();
        foreach (char caractere in nome)
        {
            if ("[]:*?/\\".IndexOf(caractere) < 0)
            {
                resultado.Append(caractere);
            }
        }
        string limpo = resultado.ToString().Trim();
        if (limpo.Length == 0)
        {
            return "Planilha";
        }
        if (limpo.Length > 31)
        {
            return limpo.Substring(0, 31);
        }
        return limpo;
    }

    private static string TiposDeConteudo()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
            + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
            + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
            + "</Types>";
    }

    private static string RelacoesRaiz()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
            + "</Relationships>";
    }

    private string Pasta()
    {
        // O filtro do cabeçalho precisa do nome definido _FilterDatabase para o Excel reconhecer
        string intervalo = "'" + _nomeAba.Replace("'", "''") + "'!$A$1:$" + LetrasColuna(_colunas.Count - 1) + "$" + Math.Max(1, _linhas.Count + 1);
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + "<sheets><sheet name=\"" + System.Security.SecurityElement.Escape(_nomeAba) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>"
            + "<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">"
            + System.Security.SecurityElement.Escape(intervalo) + "</definedName></definedNames>"
            + "</workbook>";
    }

    private static string LetrasColuna(int coluna)
    {
        string referencia = Referencia(coluna, 1);
        return referencia.Substring(0, referencia.Length - 1);
    }

    private static string RelacoesPasta()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
            + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
            + "</Relationships>";
    }

    private static string Estilos()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
            + "<numFmts count=\"3\">"
            + "<numFmt numFmtId=\"164\" formatCode=\"&quot;R$&quot;\\ #,##0.00\"/>"
            + "<numFmt numFmtId=\"165\" formatCode=\"dd/mm/yyyy\"/>"
            + "<numFmt numFmtId=\"166\" formatCode=\"dd/mm/yyyy\\ hh:mm\"/>"
            + "</numFmts>"
            + "<fonts count=\"2\">"
            + "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>"
            + "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>"
            + "</fonts>"
            + "<fills count=\"3\">"
            + "<fill><patternFill patternType=\"none\"/></fill>"
            + "<fill><patternFill patternType=\"gray125\"/></fill>"
            + "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEEF2F7\"/><bgColor indexed=\"64\"/></patternFill></fill>"
            + "</fills>"
            + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
            + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
            + "<cellXfs count=\"5\">"
            + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
            + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>"
            + "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
            + "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
            + "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
            + "</cellXfs>"
            + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
            + "</styleSheet>";
    }
}
