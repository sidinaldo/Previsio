using Previsio.Core.Entidades;
using Xunit;
using Previsio.Aplicacao.LeituraEmail;

namespace Previsio.Tests.Aplicacao.LeituraEmail;

public class CaixasDeEmailTests
{
    private static Cliente Rede(string host, string usuario)
    {
        var rede = new Cliente();
        rede.Id = 1;
        rede.ImapHost = host;
        rede.ImapUsuario = usuario;
        return rede;
    }

    private static Empresa Empresa(Cliente rede, int id, string host, string usuario, bool ativa)
    {
        var empresa = new Empresa();
        empresa.Id = id;
        empresa.ClienteId = rede.Id;
        empresa.ImapHost = host;
        empresa.ImapUsuario = usuario;
        empresa.Ativo = ativa;
        rede.Empresas.Add(empresa);
        return empresa;
    }

    [Fact]
    public void SoARede_UmaCaixa()
    {
        Cliente rede = Rede("imap.gmail.com", "notas@rede.com.br");
        Empresa(rede, 10, "", "", true);

        List<CaixaParaLer> caixas = CaixasDeEmail.Montar(rede);

        Assert.Single(caixas);
        Assert.Null(caixas[0].Empresa);
    }

    [Fact]
    public void RedeEEmpresaComEmailProprio_LeAsDuas()
    {
        Cliente rede = Rede("imap.gmail.com", "notas@rede.com.br");
        Empresa(rede, 10, "imap.gmail.com", "filial@rede.com.br", true);

        List<CaixaParaLer> caixas = CaixasDeEmail.Montar(rede);

        Assert.Equal(2, caixas.Count);
        Assert.Null(caixas[0].Empresa);
        Assert.Equal(10, caixas[1].EmpresaId);
        Assert.Equal("filial@rede.com.br", caixas[1].Dados.ImapUsuario);
    }

    [Fact]
    public void MesmaCaixaDaRede_NaoLeDuasVezes()
    {
        Cliente rede = Rede("imap.gmail.com", "notas@rede.com.br");
        Empresa(rede, 10, "IMAP.gmail.com", "Notas@Rede.com.br", true);
        Empresa(rede, 11, "imap.gmail.com", "filial@rede.com.br", true);
        Empresa(rede, 12, "imap.gmail.com", "filial@rede.com.br", true);

        List<CaixaParaLer> caixas = CaixasDeEmail.Montar(rede);

        Assert.Equal(2, caixas.Count);
        Assert.Equal(11, caixas[1].EmpresaId);
    }

    [Fact]
    public void EmpresaInativa_NaoELida()
    {
        Cliente rede = Rede("imap.gmail.com", "notas@rede.com.br");
        Empresa(rede, 10, "imap.gmail.com", "filial@rede.com.br", false);

        Assert.Single(CaixasDeEmail.Montar(rede));
    }

    [Fact]
    public void RedeSemEmail_LeSoAsDasEmpresas()
    {
        Cliente rede = Rede("", "");
        Empresa(rede, 10, "imap.gmail.com", "filial@rede.com.br", true);

        List<CaixaParaLer> caixas = CaixasDeEmail.Montar(rede);

        Assert.Single(caixas);
        Assert.Equal(10, caixas[0].EmpresaId);
    }
}
