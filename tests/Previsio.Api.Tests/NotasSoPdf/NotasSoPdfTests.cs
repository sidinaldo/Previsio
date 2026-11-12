using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;
using Previsio.Api.NotasSoPdf;
using Previsio.Api.Sefaz;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Api.Tests.Sefaz;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Infra.Seguranca;

namespace Previsio.Api.Tests.NotasSoPdf;

public class NotasSoPdfTests
{
    // Nota de exemplo: emitida para o CNPJ 11222333000262 (Centro), duas duplicatas
    private const string Chave = "31261171295348000100550040000920011289748350";

    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private readonly SefazDeTeste _sefaz = new SefazDeTeste();
    private readonly EnvioEmailDeTeste _email = new EnvioEmailDeTeste();

    private Cliente _rede = null!;
    private Empresa _matriz = null!;
    private Empresa _centro = null!;

    private static string XmlExemplo()
    {
        return File.ReadAllText(ChaveETextoPdfTests.Exemplo("nfe-92001.xml"));
    }

    private static byte[] PdfExemplo()
    {
        return File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("danfe-chromium.pdf"));
    }

    private async Task CadastrarRede(bool certificadoNaMatriz)
    {
        _rede = new Cliente();
        _rede.Nome = "Rede Auto Center";
        _rede.ImapUsuario = "notas@autocenter.com.br";
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _matriz = Empresa("Auto Center Zona Norte", "11222333000181");
        _centro = Empresa("Auto Center Centro", "11222333000262");
        await _db.SaveChangesAsync();
        if (certificadoNaMatriz)
        {
            Resultado<EmpresaDto> salvo = await ServicosDeTeste.Certificados(_db, _protetor).SalvarAsync(
                _matriz.Id, CertificadoDeTeste.Pfx("11222333000181"), CertificadoDeTeste.Senha, "RN", CancellationToken.None);
            Assert.True(salvo.Sucesso);
        }
    }

    private Empresa Empresa(string nome, string cnpj)
    {
        var empresa = new Empresa();
        empresa.ClienteId = _rede.Id;
        empresa.Nome = nome;
        empresa.Cnpj = cnpj;
        _db.Empresas.Add(empresa);
        return empresa;
    }

    private RegistroNotaSoPdf Registro()
    {
        return new RegistroNotaSoPdf(_db);
    }

    private BuscaXmlSefazService Busca()
    {
        return new BuscaXmlSefazService(_db, _sefaz, ServicosDeTeste.Certificados(_db, _protetor), new RegistroNotaService(_db),
            new LeitorNFe(), _relogio, NullLogger<BuscaXmlSefazService>.Instance);
    }

    private NotasSoPdfService Acoes()
    {
        return new NotasSoPdfService(_db, new RegistroNotaService(_db), new LeitorNFe(), _email, _relogio, NullLogger<NotasSoPdfService>.Instance);
    }

    private static EmailSemXml Email(string assunto, params PdfDoEmail[] pdfs)
    {
        var email = new EmailSemXml();
        email.MensagemId = Guid.NewGuid().ToString() + "@fornecedor.com.br";
        email.Remetente = "faturamento@alfa.com.br";
        email.NomeRemetente = "Alfa Distribuidora";
        email.Assunto = assunto;
        email.Origem = "Alfa Distribuidora <faturamento@alfa.com.br> | " + assunto;
        email.Pdfs.AddRange(pdfs);
        return email;
    }

    private static PdfDoEmail Pdf(string nome, byte[] conteudo)
    {
        return new PdfDoEmail { Nome = nome, Conteudo = conteudo };
    }

    private async Task<NotaSoPdf> RegistrarDanfe()
    {
        await Registro().RegistrarAsync(_rede.Id, null, Email("NF-e 92001", Pdf("danfe.pdf", PdfExemplo())), CancellationToken.None);
        return _db.NotasSoPdf.Single();
    }

    private ClaimsPrincipal Financeiro()
    {
        return UsuarioDeTeste.DoCliente(_rede.Id).HttpContext.User;
    }

    // ---------- Leitura do e-mail ----------

    [Fact]
    public async Task Danfe_sem_xml_vira_pendencia_com_a_chave_lida_do_pdf()
    {
        await CadastrarRede(true);

        NotaSoPdf nota = await RegistrarDanfe();

        Assert.Equal(Chave, nota.ChaveAcesso);
        Assert.Equal("PDF", nota.OrigemChave);
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, nota.Situacao);
        Assert.Equal("faturamento@alfa.com.br", nota.Remetente);
        Assert.NotEmpty(nota.Pdf);
    }

    [Fact]
    public async Task Chave_no_nome_do_arquivo_e_mesma_mensagem_lida_de_novo_nao_duplica()
    {
        await CadastrarRede(true);
        EmailSemXml email = Email("Sua nota", Pdf(Chave + "-nfe.pdf", new byte[] { 1, 2, 3 }));

        Assert.Equal(1, await Registro().RegistrarAsync(_rede.Id, null, email, CancellationToken.None));
        Assert.Equal(0, await Registro().RegistrarAsync(_rede.Id, null, email, CancellationToken.None));

        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal("nome do arquivo", nota.OrigemChave);
    }

    [Fact]
    public async Task Boleto_e_pdf_que_nao_e_nota_sao_ignorados_e_nota_sem_chave_fica_para_alguem()
    {
        await CadastrarRede(true);
        byte[] semTexto = new byte[] { 1, 2, 3 };

        Assert.Equal(0, await Registro().RegistrarAsync(_rede.Id, null, Email("Boleto de novembro", Pdf("boleto.pdf", semTexto)), CancellationToken.None));
        Assert.Equal(0, await Registro().RegistrarAsync(_rede.Id, null, Email("Pedido 123", Pdf("pedido.pdf", semTexto)), CancellationToken.None));
        Assert.Equal(1, await Registro().RegistrarAsync(_rede.Id, null, Email("Nota fiscal 5521", Pdf("documento.pdf", semTexto)), CancellationToken.None));

        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.SemChave, nota.Situacao);
        Assert.Null(nota.ChaveAcesso);
        Assert.Equal(RegistroNotaSoPdf.MotivoSemChave, nota.Motivo);
    }

    [Fact]
    public async Task Pdf_de_nota_que_ja_chegou_em_xml_nao_vira_pendencia()
    {
        await CadastrarRede(true);
        await new RegistroNotaService(_db).RegistrarAsync(_rede.Id, new LeitorNFe().Ler(XmlExemplo())!, "xml", null, CancellationToken.None);

        int registradas = await Registro().RegistrarAsync(_rede.Id, null, Email("NF-e 92001", Pdf("danfe.pdf", PdfExemplo())), CancellationToken.None);

        Assert.Equal(0, registradas);
    }

    // ---------- Busca na SEFAZ ----------

    [Fact]
    public async Task Sefaz_resumo_depois_ciencia_depois_xml_completo_vira_nota_normal()
    {
        await CadastrarRede(true);
        NotaSoPdf pendente = await RegistrarDanfe();
        _sefaz.Responder("11222333000262", SefazDeTeste.Resumo(), SefazDeTeste.Completo(XmlExemplo()));

        await Busca().ProcessarPendentesAsync(CancellationToken.None);

        NotaSoPdf depoisDaCiencia = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, depoisDaCiencia.Situacao);
        Assert.NotNull(depoisDaCiencia.CienciaEnviadaEm);
        Assert.Equal(_centro.Id, depoisDaCiencia.EmpresaDestinoId);
        Assert.Equal(_relogio.Agora.UtcDateTime.AddMinutes(5), depoisDaCiencia.ProximaTentativaEm);
        // A matriz (sem documento para ela) foi consultada antes; a ciência saiu pelo CNPJ do Centro, com o certificado da matriz
        Assert.Contains("consulta 11222333000262 RN cert:11222333000181", _sefaz.Chamadas);
        Assert.Contains("ciencia 11222333000262", _sefaz.Chamadas);

        // Antes da hora marcada não consulta de novo
        int chamadas = _sefaz.Chamadas.Count;
        await Busca().ProcessarPendentesAsync(CancellationToken.None);
        Assert.Equal(chamadas, _sefaz.Chamadas.Count);

        _relogio.Agora = _relogio.Agora.AddMinutes(6);
        await Busca().ProcessarPendentesAsync(CancellationToken.None);

        NotaSoPdf resolvida = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.Resolvida, resolvida.Situacao);
        Assert.Equal("XML baixado da SEFAZ", resolvida.ResolvidaPor);
        NotaFiscal nota = _db.NotasFiscais.Single();
        Assert.Equal(resolvida.NotaFiscalId, nota.Id);
        Assert.Equal(Chave, nota.ChaveAcesso);
        Assert.Equal(_centro.Id, nota.EmpresaId);
        Assert.Equal(StatusNota.Recebida, nota.Status);
        Assert.Equal(pendente.OrigemEmail, nota.OrigemEmail);
        // Já confirmada a loja, a segunda consulta vai direto nela
        Assert.Equal("consulta 11222333000262 RN cert:11222333000181", _sefaz.Chamadas[_sefaz.Chamadas.Count - 1]);
    }

    [Fact]
    public async Task Sem_certificado_na_rede_a_nota_espera_e_volta_para_a_fila_quando_cadastrarem()
    {
        await CadastrarRede(false);
        await RegistrarDanfe();

        await Busca().ProcessarPendentesAsync(CancellationToken.None);

        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.SemCertificado, nota.Situacao);
        Assert.Empty(_sefaz.Chamadas);

        await ServicosDeTeste.Certificados(_db, _protetor).SalvarAsync(
            _centro.Id, CertificadoDeTeste.Pfx("11222333000262"), CertificadoDeTeste.Senha, "SP", CancellationToken.None);
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, _db.NotasSoPdf.Single().Situacao);
    }

    [Fact]
    public async Task Nao_encontrada_tenta_com_espera_crescente_e_desiste_depois_de_6_vezes()
    {
        await CadastrarRede(true);
        await RegistrarDanfe();

        for (int tentativa = 1; tentativa <= BuscaXmlSefazService.MaximoTentativas; tentativa++)
        {
            await Busca().ProcessarPendentesAsync(CancellationToken.None);
            NotaSoPdf nota = _db.NotasSoPdf.Single();
            Assert.Equal(tentativa, nota.TentativasSefaz);
            if (nota.ProximaTentativaEm.HasValue)
            {
                _relogio.Agora = new DateTimeOffset(nota.ProximaTentativaEm.Value, TimeSpan.Zero);
            }
        }

        NotaSoPdf desistiu = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.NaoResolvida, desistiu.Situacao);
        Assert.StartsWith("Não consegui o XML na SEFAZ depois de 6 tentativas.", desistiu.Motivo);
        Assert.Contains("137 - Nenhum documento localizado", desistiu.Motivo);
    }

    [Fact]
    public async Task Consumo_indevido_espera_uma_hora_sem_gastar_tentativa()
    {
        await CadastrarRede(true);
        await RegistrarDanfe();
        _sefaz.Responder("11222333000181", SefazDeTeste.Retorno("656", "Rejeicao: Consumo Indevido"));

        await Busca().ProcessarPendentesAsync(CancellationToken.None);

        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal(0, nota.TentativasSefaz);
        Assert.Equal(_relogio.Agora.UtcDateTime.AddMinutes(65), nota.ProximaTentativaEm);
    }

    [Fact]
    public async Task Sefaz_fora_do_ar_conta_tentativa_e_tenta_depois()
    {
        await CadastrarRede(true);
        await RegistrarDanfe();
        _sefaz.FalharConexao = true;

        await Busca().ProcessarPendentesAsync(CancellationToken.None);

        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal(1, nota.TentativasSefaz);
        Assert.StartsWith("Não consegui falar com a SEFAZ", nota.Motivo);
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, nota.Situacao);
    }

    // ---------- Ações do financeiro ----------

    [Fact]
    public async Task Anexar_o_xml_registra_a_nota_e_fecha_a_pendencia()
    {
        await CadastrarRede(false);
        NotaSoPdf pendente = await RegistrarDanfe();

        Resultado outroXml = await Acoes().AnexarXmlAsync(pendente.Id, "<qualquer/>", Financeiro(), CancellationToken.None);
        Assert.Equal(NotasSoPdfService.MensagemXmlInvalido, outroXml.Mensagem);

        Resultado anexado = await Acoes().AnexarXmlAsync(pendente.Id, XmlExemplo(), Financeiro(), CancellationToken.None);

        Assert.True(anexado.Sucesso);
        NotaSoPdf resolvida = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.Resolvida, resolvida.Situacao);
        Assert.Equal(_db.NotasFiscais.Single().Id, resolvida.NotaFiscalId);
        Assert.Empty(await Acoes().ListarAsync(_rede.Id, null, CancellationToken.None));
        Resultado deNovo = await Acoes().AnexarXmlAsync(pendente.Id, XmlExemplo(), Financeiro(), CancellationToken.None);
        Assert.Equal(NotasSoPdfService.MensagemJaResolvida, deNovo.Mensagem);
    }

    [Fact]
    public async Task Xml_de_outra_nota_e_recusado()
    {
        await CadastrarRede(false);
        await Registro().RegistrarAsync(_rede.Id, null,
            Email("NF", Pdf("31261171295348000100550040000920021289748357.pdf", new byte[] { 1 })), CancellationToken.None);
        NotaSoPdf pendente = _db.NotasSoPdf.Single();
        Assert.Equal("31261171295348000100550040000920021289748357", pendente.ChaveAcesso);

        Resultado resultado = await Acoes().AnexarXmlAsync(pendente.Id, XmlExemplo(), Financeiro(), CancellationToken.None);

        Assert.Equal("Esse XML é de outra nota (NF 92001). O PDF é da NF 92002.", resultado.Mensagem);
    }

    [Fact]
    public async Task Informar_a_chave_poe_a_nota_na_fila_da_sefaz()
    {
        await CadastrarRede(true);
        await Registro().RegistrarAsync(_rede.Id, null, Email("Nota fiscal", Pdf("documento.pdf", new byte[] { 1 })), CancellationToken.None);
        NotaSoPdf pendente = _db.NotasSoPdf.Single();

        Resultado invalida = await Acoes().InformarChaveAsync(pendente.Id, "1234", Financeiro(), CancellationToken.None);
        Assert.Equal(NotasSoPdfService.MensagemChaveInvalida, invalida.Mensagem);

        string agrupada = string.Join(" ", Enumerable.Range(0, 11).Select(i => Chave.Substring(i * 4, 4)));
        Resultado ok = await Acoes().InformarChaveAsync(pendente.Id, agrupada, Financeiro(), CancellationToken.None);

        Assert.True(ok.Sucesso);
        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal(Chave, nota.ChaveAcesso);
        Assert.Equal("informada", nota.OrigemChave);
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, nota.Situacao);
        NotaSoPdfDto dto = Assert.Single(await Acoes().ListarAsync(_rede.Id, null, CancellationToken.None));
        Assert.Equal("92001", dto.NumeroNota);
        Assert.Equal("71295348000100", dto.CnpjFornecedor);
    }

    [Fact]
    public async Task Pedir_o_xml_responde_ao_fornecedor_com_a_caixa_das_notas()
    {
        await CadastrarRede(false);
        NotaSoPdf pendente = await RegistrarDanfe();

        Resultado resultado = await Acoes().PedirXmlAsync(pendente.Id, Financeiro(), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Previsio.Aplicacao.Abstracoes.MensagemEmail email = Assert.Single(_email.Enviados);
        Assert.Equal("faturamento@alfa.com.br", email.Para);
        Assert.Equal("notas@autocenter.com.br", email.ResponderPara);
        Assert.Equal("Pedido do XML: NF-e 92001", email.Assunto);
        Assert.Contains("Recebemos a NF-e 92001 em PDF", email.Texto);
        Assert.Contains("envie o XML da nota para notas@autocenter.com.br", email.Texto);
        Assert.NotNull(_db.NotasSoPdf.Single().XmlPedidoEm);

        _email.Configurado = false;
        Resultado semSmtp = await Acoes().PedirXmlAsync(pendente.Id, Financeiro(), CancellationToken.None);
        Assert.Equal(NotasSoPdfService.MensagemSmtpNaoConfigurado, semSmtp.Mensagem);
    }

    [Fact]
    public async Task Ja_lancei_no_bling_fecha_e_usuario_de_outra_rede_nao_ve()
    {
        await CadastrarRede(false);
        NotaSoPdf pendente = await RegistrarDanfe();

        Resultado deOutraRede = await Acoes().MarcarLancadaAsync(pendente.Id, null, UsuarioDeTeste.DoCliente(_rede.Id + 99).HttpContext.User, CancellationToken.None);
        Assert.Equal(TipoResultado.NaoEncontrado, deOutraRede.Tipo);

        Resultado ok = await Acoes().MarcarLancadaAsync(pendente.Id, "conta 4512 no Bling", Financeiro(), CancellationToken.None);

        Assert.True(ok.Sucesso);
        NotaSoPdf nota = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.LancadaManualmente, nota.Situacao);
        Assert.Equal("conta 4512 no Bling", nota.Observacao);
    }

    [Fact]
    public async Task Nota_sem_chave_aparece_no_sininho_e_some_quando_resolvida()
    {
        await CadastrarRede(false);
        await Registro().RegistrarAsync(_rede.Id, null, Email("Nota fiscal 5521", Pdf("documento.pdf", new byte[] { 1 })), CancellationToken.None);
        IConfiguration configuracao = new ConfigurationBuilder().Build();
        var avisos = new AvisosService(_db, _email, configuracao, _relogio, NullLogger<AvisosService>.Instance);

        await avisos.AtualizarAsync(_rede.Id, CancellationToken.None);
        Aviso aviso = _db.Avisos.Single(a => a.Chave.StartsWith("pdf:"));
        Assert.Equal("Nota só em PDF de Alfa Distribuidora", aviso.Titulo);
        Assert.Equal("/pages/notas-pendentes", aviso.Link);

        NotaSoPdf pendente = _db.NotasSoPdf.Single();
        await Acoes().MarcarLancadaAsync(pendente.Id, null, Financeiro(), CancellationToken.None);
        await avisos.AtualizarAsync(_rede.Id, CancellationToken.None);
        Assert.NotNull(_db.Avisos.Single(a => a.Chave.StartsWith("pdf:")).ResolvidoEm);
    }
}
