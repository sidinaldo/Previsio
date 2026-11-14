using Previsio.Core.NFe;
using Xunit;

namespace Previsio.Tests.Core.NFe;

public class LeitorNFeTipoPagamentoTests
{
    private readonly LeitorNFe _leitor = new LeitorNFe();

    private static string LerExemplo(string nome)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Exemplos", nome));
    }

    private static string ComPagamento(string xml, string detalhes)
    {
        return xml.Replace("</cobr>", "</cobr><pag>" + detalhes + "</pag>");
    }

    [Fact]
    public void Le_o_meio_de_pagamento_do_xml()
    {
        NotaFiscalXml? nota = _leitor.Ler(LerExemplo("nfe-92001.xml"));

        Assert.NotNull(nota);
        Assert.Equal(15, nota!.TipoPagamento);
    }

    [Fact]
    public void Nota_sem_pag_fica_sem_tipo()
    {
        NotaFiscalXml? nota = _leitor.Ler(LerExemplo("nfe-com-duplicatas.xml"));

        Assert.NotNull(nota);
        Assert.Null(nota!.TipoPagamento);
    }

    [Fact]
    public void Vale_o_pagamento_de_maior_valor_e_90_e_99_nao_contam()
    {
        string xml = ComPagamento(LerExemplo("nfe-com-duplicatas.xml"),
            "<detPag><tPag>99</tPag><vPag>9000.00</vPag></detPag>"
            + "<detPag><tPag>17</tPag><vPag>100.00</vPag></detPag>"
            + "<detPag><tPag>15</tPag><vPag>500.00</vPag></detPag>");
        Assert.Equal(15, _leitor.Ler(xml)!.TipoPagamento);

        string semPagamento = ComPagamento(LerExemplo("nfe-com-duplicatas.xml"),
            "<detPag><tPag>90</tPag><vPag>0.00</vPag></detPag>");
        Assert.Null(_leitor.Ler(semPagamento)!.TipoPagamento);
    }
}
