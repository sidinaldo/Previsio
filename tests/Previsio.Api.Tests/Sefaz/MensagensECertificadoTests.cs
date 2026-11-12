using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.DataProtection;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Infra.Sefaz;
using Previsio.Infra.Seguranca;
using Previsio.Core.Sefaz;
using Previsio.Aplicacao.Certificados;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Tests.Sefaz;

public class MensagensECertificadoTests
{
    private const string Chave = "31261171295348000100550040000920011289748350";

    [Fact]
    public void Consulta_por_chave_leva_ambiente_uf_cnpj_e_chave()
    {
        string xml = MensagensSefaz.ConsultaPorChave("1", "35", "11222333000181", Chave);
        var documento = new XmlDocument();
        documento.LoadXml(xml);
        var ns = new XmlNamespaceManager(documento.NameTable);
        ns.AddNamespace("n", MensagensSefaz.NamespaceNfe);
        XmlNode dist = documento.SelectSingleNode("//n:distDFeInt", ns)!;
        Assert.Equal("1.01", dist.Attributes!["versao"]!.Value);
        Assert.Equal("1", dist.SelectSingleNode("n:tpAmb", ns)!.InnerText);
        Assert.Equal("35", dist.SelectSingleNode("n:cUFAutor", ns)!.InnerText);
        Assert.Equal("11222333000181", dist.SelectSingleNode("n:CNPJ", ns)!.InnerText);
        Assert.Equal(Chave, dist.SelectSingleNode("n:consChNFe/n:chNFe", ns)!.InnerText);
    }

    [Fact]
    public void Evento_de_ciencia_sai_assinado_e_a_assinatura_confere()
    {
        using X509Certificate2 certificado = CertificadoA1.Abrir(CertificadoDeTeste.Pfx("11222333000181"), CertificadoDeTeste.Senha);
        var quando = new DateTimeOffset(2026, 11, 11, 18, 30, 0, TimeSpan.FromHours(-3));

        string soap = MensagensSefaz.EventoCienciaAssinado("2", "11222333000181", Chave, quando, certificado);

        var documento = new XmlDocument();
        documento.PreserveWhitespace = true;
        documento.LoadXml(soap);
        var ns = new XmlNamespaceManager(documento.NameTable);
        ns.AddNamespace("n", MensagensSefaz.NamespaceNfe);
        ns.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
        XmlElement infEvento = (XmlElement)documento.SelectSingleNode("//n:infEvento", ns)!;
        Assert.Equal("ID210210" + Chave + "01", infEvento.GetAttribute("Id"));
        Assert.Equal("91", infEvento.SelectSingleNode("n:cOrgao", ns)!.InnerText);
        Assert.Equal("2026-11-11T18:30:00-03:00", infEvento.SelectSingleNode("n:dhEvento", ns)!.InnerText);
        Assert.Equal("Ciencia da Operacao", infEvento.SelectSingleNode("n:detEvento/n:descEvento", ns)!.InnerText);

        // A SEFAZ valida o lote (envEvento) fora do envelope SOAP: confere a assinatura do mesmo jeito
        var lote = new XmlDocument();
        lote.PreserveWhitespace = true;
        lote.LoadXml(documento.SelectSingleNode("//n:envEvento", ns)!.OuterXml);
        XmlElement evento = (XmlElement)lote.SelectSingleNode("//n:evento", ns)!;
        XmlElement assinatura = (XmlElement)evento.SelectSingleNode("ds:Signature", ns)!;
        Assert.Equal("#ID210210" + Chave + "01", assinatura.SelectSingleNode("ds:SignedInfo/ds:Reference", ns)!.Attributes!["URI"]!.Value);
        Assert.Equal(SignedXml.XmlDsigRSASHA1Url, assinatura.SelectSingleNode("ds:SignedInfo/ds:SignatureMethod", ns)!.Attributes!["Algorithm"]!.Value);
        var conferencia = new SignedXml(evento);
        conferencia.LoadXml(assinatura);
        Assert.True(conferencia.CheckSignature(certificado, true));
    }

    [Fact]
    public void Le_o_retorno_da_distribuicao_e_descompacta_os_documentos()
    {
        string procNFe = "<nfeProc xmlns=\"http://www.portalfiscal.inf.br/nfe\"><NFe/></nfeProc>";
        string resposta = "<?xml version=\"1.0\" encoding=\"utf-8\"?><soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body>"
            + "<nfeDistDFeInteresseResponse xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe\"><nfeDistDFeInteresseResult>"
            + "<retDistDFeInt versao=\"1.01\" xmlns=\"http://www.portalfiscal.inf.br/nfe\"><tpAmb>1</tpAmb><verAplic>1.7.6</verAplic>"
            + "<cStat>138</cStat><xMotivo>Documento localizado</xMotivo><dhResp>2026-11-11T18:31:00-03:00</dhResp>"
            + "<loteDistDFeInt><docZip NSU=\"000000000000101\" schema=\"procNFe_v4.00.xsd\">" + Gzip(procNFe) + "</docZip></loteDistDFeInt>"
            + "</retDistDFeInt></nfeDistDFeInteresseResult></nfeDistDFeInteresseResponse></soap:Body></soap:Envelope>";

        RetornoSefaz retorno = MensagensSefaz.LerRetornoDistribuicao(resposta);

        Assert.True(retorno.DocumentoLocalizado);
        Assert.Equal("138 - Documento localizado", retorno.Descricao);
        DocumentoDistribuido documento = Assert.Single(retorno.Documentos);
        Assert.True(documento.NotaCompleta);
        Assert.Equal(procNFe, documento.Xml);
    }

    [Fact]
    public void Le_o_retorno_do_evento_e_aceita_ciencia_ja_registrada()
    {
        string Resposta(string cStat, string motivo)
        {
            return "<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\"><soap:Body>"
                + "<nfeRecepcaoEventoNFResult xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4\">"
                + "<retEnvEvento versao=\"1.00\" xmlns=\"http://www.portalfiscal.inf.br/nfe\"><cStat>128</cStat><xMotivo>Lote de Evento Processado</xMotivo>"
                + "<retEvento versao=\"1.00\"><infEvento><cStat>" + cStat + "</cStat><xMotivo>" + motivo + "</xMotivo></infEvento></retEvento>"
                + "</retEnvEvento></nfeRecepcaoEventoNFResult></soap:Body></soap:Envelope>";
        }

        Assert.True(MensagensSefaz.LerRetornoEvento(Resposta("135", "Evento registrado e vinculado a NF-e")).EventoRegistrado);
        Assert.True(MensagensSefaz.LerRetornoEvento(Resposta("573", "Rejeicao: Duplicidade de evento")).EventoRegistrado);
        RetornoSefaz recusado = MensagensSefaz.LerRetornoEvento(Resposta("594", "Rejeicao: O numero sequencial do evento e maior que o permitido"));
        Assert.False(recusado.EventoRegistrado);
        Assert.Equal("594", recusado.Status);
    }

    [Fact]
    public async Task Certificado_da_mesma_raiz_e_salvo_criptografado_e_com_os_dados_para_a_tela()
    {
        PrevisioDbContext db = BancoEmMemoria.Criar();
        var protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
        Empresa filial = await Empresa(db, "11222333000262");
        CertificadosService servico = ServicosDeTeste.Certificados(db, protetor);

        // e-CNPJ da matriz serve para a filial (mesma raiz 11222333)
        Resultado<EmpresaDto> resultado = await servico.SalvarAsync(
            filial.Id, CertificadoDeTeste.Pfx("11222333000181"), CertificadoDeTeste.Senha, "rn", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor!.CertificadoCadastrado);
        Assert.Equal("RN", resultado.Valor.Uf);
        Assert.Equal("AUTO CENTER LTDA (11.222.333/0001-81)", resultado.Valor.CertificadoTitular);
        Empresa gravada = db.Empresas.Single(e => e.Id == filial.Id);
        Assert.DoesNotContain(CertificadoDeTeste.Senha, gravada.CertificadoSenhaProtegida);
        using X509Certificate2? aberto = servico.Abrir(gravada);
        Assert.NotNull(aberto);
        Assert.True(aberto!.HasPrivateKey);

        Resultado<EmpresaDto> removido = await servico.RemoverAsync(filial.Id, CancellationToken.None);
        Assert.False(removido.Valor!.CertificadoCadastrado);
        Assert.Null(servico.Abrir(db.Empresas.Single(e => e.Id == filial.Id)));
    }

    [Fact]
    public async Task Certificado_recusado_com_motivo_claro()
    {
        PrevisioDbContext db = BancoEmMemoria.Criar();
        var protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
        Empresa matriz = await Empresa(db, "11222333000181");
        CertificadosService servico = ServicosDeTeste.Certificados(db, protetor);
        byte[] pfx = CertificadoDeTeste.Pfx("11222333000181");

        Resultado<EmpresaDto> senhaErrada = await servico.SalvarAsync(matriz.Id, pfx, "outra", "SP", CancellationToken.None);
        Assert.Equal(CertificadoA1.MensagemSenhaInvalida, senhaErrada.Mensagem);

        Resultado<EmpresaDto> semUf = await servico.SalvarAsync(matriz.Id, pfx, CertificadoDeTeste.Senha, "XX", CancellationToken.None);
        Assert.Equal(CertificadosService.MensagemUfInvalida, semUf.Mensagem);

        Resultado<EmpresaDto> outraEmpresa = await servico.SalvarAsync(
            matriz.Id, CertificadoDeTeste.Pfx("99888777000155"), CertificadoDeTeste.Senha, "SP", CancellationToken.None);
        Assert.StartsWith("O certificado é do CNPJ 99.888.777/0001-55", outraEmpresa.Mensagem);

        Resultado<EmpresaDto> vencido = await servico.SalvarAsync(
            matriz.Id, CertificadoDeTeste.Pfx("11222333000181", DateTimeOffset.UtcNow.AddDays(-2)), CertificadoDeTeste.Senha, "SP", CancellationToken.None);
        Assert.StartsWith("O certificado venceu em", vencido.Mensagem);

        Assert.False(db.Empresas.Single(e => e.Id == matriz.Id).TemCertificado);
    }

    private static async Task<Empresa> Empresa(PrevisioDbContext db, string cnpj)
    {
        var rede = new Cliente();
        rede.Nome = "Rede";
        db.Clientes.Add(rede);
        await db.SaveChangesAsync();
        var empresa = new Empresa();
        empresa.ClienteId = rede.Id;
        empresa.Nome = "Loja";
        empresa.Cnpj = cnpj;
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        return empresa;
    }

    private static string Gzip(string texto)
    {
        using (var saida = new MemoryStream())
        {
            using (var gzip = new GZipStream(saida, CompressionLevel.Optimal, true))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(texto);
                gzip.Write(bytes, 0, bytes.Length);
            }
            return Convert.ToBase64String(saida.ToArray());
        }
    }
}
