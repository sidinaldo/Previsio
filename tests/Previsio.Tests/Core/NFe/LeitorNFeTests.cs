using Previsio.Core.NFe;
using Xunit;

namespace Previsio.Tests.Core.NFe;

public class LeitorNFeTests
{
    private readonly LeitorNFe _leitor = new LeitorNFe();

    private static string LerExemplo(string nome)
    {
        string caminho = Path.Combine(AppContext.BaseDirectory, "Exemplos", nome);
        return File.ReadAllText(caminho);
    }

    [Fact]
    public void Le_dados_principais_da_nota()
    {
        NotaFiscalXml? nota = _leitor.Ler(LerExemplo("nfe-com-duplicatas.xml"));

        Assert.NotNull(nota);
        Assert.Equal("24251112345678000199550010000482131000482130", nota!.ChaveAcesso);
        Assert.Equal("48213", nota.Numero);
        Assert.Equal("1", nota.Serie);
        Assert.Equal(new DateTime(2026, 11, 6), nota.DataEmissao);
        Assert.Equal(6480.00m, nota.ValorTotal);
        Assert.Equal("12345678000199", nota.CnpjFornecedor);
        Assert.Equal("ROTA SUL PECAS LTDA", nota.NomeFornecedor);
        Assert.Equal("98765432000110", nota.CnpjDestinatario);
    }

    [Fact]
    public void Le_todas_as_duplicatas()
    {
        NotaFiscalXml? nota = _leitor.Ler(LerExemplo("nfe-com-duplicatas.xml"));

        Assert.NotNull(nota);
        Assert.Equal(2, nota!.Duplicatas.Count);
        Assert.Equal("001", nota.Duplicatas[0].Numero);
        Assert.Equal(new DateTime(2026, 11, 12), nota.Duplicatas[0].Vencimento);
        Assert.Equal(3240.00m, nota.Duplicatas[0].Valor);
        Assert.Equal(new DateTime(2026, 12, 12), nota.Duplicatas[1].Vencimento);
    }

    [Fact]
    public void Retorna_null_para_xml_que_nao_e_nfe()
    {
        string evento = "<procEventoNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"><evento/></procEventoNFe>";
        Assert.Null(_leitor.Ler(evento));
    }

    [Fact]
    public void Retorna_null_para_conteudo_invalido()
    {
        Assert.Null(_leitor.Ler("isto não é xml"));
        Assert.Null(_leitor.Ler(""));
    }

    [Fact]
    public void Nota_sem_cobranca_vem_sem_duplicatas()
    {
        string xml = LerExemplo("nfe-com-duplicatas.xml");
        int inicio = xml.IndexOf("<cobr>");
        int fim = xml.IndexOf("</cobr>") + "</cobr>".Length;
        string semCobranca = xml.Remove(inicio, fim - inicio);

        NotaFiscalXml? nota = _leitor.Ler(semCobranca);

        Assert.NotNull(nota);
        Assert.Empty(nota!.Duplicatas);
    }
}
