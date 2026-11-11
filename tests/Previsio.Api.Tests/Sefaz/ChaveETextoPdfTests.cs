using System.IO.Compression;
using System.Text;
using Previsio.Api.Sefaz;
using Xunit;

namespace Previsio.Api.Tests.Sefaz;

public class ChaveETextoPdfTests
{
    // Chave da nota de exemplo (Exemplos/nfe-92001.xml e o DANFE impresso pelo Chromium)
    private const string ChaveExemplo = "31261171295348000100550040000920011289748350";

    public static string Exemplo(string nome)
    {
        return Path.Combine(AppContext.BaseDirectory, "Exemplos", nome);
    }

    [Fact]
    public void Chave_valida_confere_o_digito_e_o_modelo_55()
    {
        Assert.True(ChaveAcesso.Valida(ChaveExemplo));
        // Dígito trocado
        Assert.False(ChaveAcesso.Valida(ChaveExemplo.Substring(0, 43) + "1"));
        // Tamanho errado e letras
        Assert.False(ChaveAcesso.Valida(ChaveExemplo.Substring(0, 43)));
        Assert.False(ChaveAcesso.Valida("A" + ChaveExemplo.Substring(1)));
        Assert.Equal("92001", ChaveAcesso.Numero(ChaveExemplo));
        Assert.Equal("71295348000100", ChaveAcesso.CnpjEmitente(ChaveExemplo));
    }

    [Fact]
    public void Procura_a_chave_em_grupos_de_4_ou_colada_e_ignora_numeros_que_nao_sao_chave()
    {
        string agrupada = string.Join(" ", Enumerable.Range(0, 11).Select(i => ChaveExemplo.Substring(i * 4, 4)));
        Assert.Equal(ChaveExemplo, ChaveAcesso.Procurar("CHAVE DE ACESSO " + agrupada + " Consulta em www.nfe.fazenda.gov.br"));
        Assert.Equal(ChaveExemplo, ChaveAcesso.Procurar("NFe" + ChaveExemplo + "-procNFe.pdf"));
        Assert.Equal(ChaveExemplo, ChaveAcesso.Procurar(ChaveExemplo.Replace("5500", "5500")));
        // 44 dígitos com dígito verificador errado não valem
        Assert.Null(ChaveAcesso.Procurar(ChaveExemplo.Substring(0, 43) + "9"));
        // Número maior (45 dígitos) não é chave
        Assert.Null(ChaveAcesso.Procurar("1" + ChaveExemplo));
        Assert.Null(ChaveAcesso.Procurar(null));
    }

    [Fact]
    public void Acha_a_chave_no_danfe_impresso_pelo_navegador()
    {
        byte[] pdf = File.ReadAllBytes(Exemplo("danfe-chromium.pdf"));
        Assert.Equal(ChaveExemplo, TextoPdf.ProcurarChave(pdf));
    }

    [Fact]
    public void Acha_a_chave_em_pdf_com_texto_compactado()
    {
        string agrupada = string.Join(" ", Enumerable.Range(0, 11).Select(i => ChaveExemplo.Substring(i * 4, 4)));
        byte[] pdf = PdfSimples("BT /F1 9 Tf 10 10 Td (CHAVE DE ACESSO) Tj 0 -12 Td (" + agrupada + ") Tj ET", true);
        Assert.Equal(ChaveExemplo, TextoPdf.ProcurarChave(pdf));
    }

    [Fact]
    public void Acha_a_chave_em_fonte_cid_pelo_mapa_tounicode()
    {
        // Cada dígito desenhado com um código de 2 bytes que só o ToUnicode traduz (0x0013 = '0' ... 0x001C = '9')
        var hex = new StringBuilder();
        foreach (char digito in ChaveExemplo)
        {
            hex.Append((0x13 + (digito - '0')).ToString("X4"));
        }
        string cmap = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap\n"
            + "1 begincodespacerange <0000> <FFFF> endcodespacerange\n"
            + "1 beginbfrange <0013> <001C> <0030> endbfrange\n"
            + "endcmap CMapName currentdict /CMap defineresource pop end end";
        string conteudo = "BT /F1 9 Tf 10 10 Td <" + hex + "> Tj ET";
        byte[] pdf = PdfComDoisStreams(conteudo, cmap);
        Assert.Equal(ChaveExemplo, TextoPdf.ProcurarChave(pdf));
    }

    [Fact]
    public void Pdf_sem_chave_ou_invalido_volta_nulo()
    {
        Assert.Null(TextoPdf.ProcurarChave(PdfSimples("BT (BOLETO BANCARIO 23790.12345) Tj ET", true)));
        Assert.Null(TextoPdf.ProcurarChave(Encoding.ASCII.GetBytes("isto não é um PDF")));
        Assert.Null(TextoPdf.ProcurarChave(Array.Empty<byte>()));
    }

    private static byte[] Compactar(string texto)
    {
        using (var saida = new MemoryStream())
        {
            using (var zlib = new ZLibStream(saida, CompressionLevel.Optimal, true))
            {
                byte[] bytes = Encoding.Latin1.GetBytes(texto);
                zlib.Write(bytes, 0, bytes.Length);
            }
            return saida.ToArray();
        }
    }

    private static byte[] PdfSimples(string conteudo, bool compactar)
    {
        using (var pdf = new MemoryStream())
        {
            Escrever(pdf, "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n");
            EscreverStream(pdf, 2, conteudo, compactar);
            Escrever(pdf, "trailer\n<< /Root 1 0 R >>\n%%EOF\n");
            return pdf.ToArray();
        }
    }

    private static byte[] PdfComDoisStreams(string conteudo, string cmap)
    {
        using (var pdf = new MemoryStream())
        {
            Escrever(pdf, "%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n");
            EscreverStream(pdf, 2, conteudo, true);
            EscreverStream(pdf, 3, cmap, true);
            Escrever(pdf, "trailer\n<< /Root 1 0 R >>\n%%EOF\n");
            return pdf.ToArray();
        }
    }

    private static void EscreverStream(MemoryStream pdf, int numero, string conteudo, bool compactar)
    {
        byte[] dados = compactar ? Compactar(conteudo) : Encoding.Latin1.GetBytes(conteudo);
        string filtro = compactar ? " /Filter /FlateDecode" : "";
        Escrever(pdf, numero + " 0 obj\n<< /Length " + dados.Length + filtro + " >>\nstream\n");
        pdf.Write(dados, 0, dados.Length);
        Escrever(pdf, "\nendstream\nendobj\n");
    }

    private static void Escrever(MemoryStream pdf, string texto)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(texto);
        pdf.Write(bytes, 0, bytes.Length);
    }
}
