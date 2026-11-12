using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Tests.Apoio;
using Previsio.Tests.Core.Sefaz;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.EmailsIgnorados;
using Previsio.Aplicacao.LeituraEmail;
using Previsio.Aplicacao.NotasSoPdf;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Infra.Seguranca;
using Xunit;

namespace Previsio.Tests.Aplicacao.LeituraEmail;

public class LeituraEmailServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
    private readonly SessaoImapDeTeste _caixa = new SessaoImapDeTeste();
    private Cliente _rede = null!;
    private Empresa _loja = null!;

    private async Task CadastrarRede(long? ultimoUid = 10, long? validade = 7)
    {
        _rede = new Cliente
        {
            Nome = "Rede", ImapHost = "imap.gmail.com", ImapPorta = 993, ImapSsl = true, ImapUsuario = "notas@rede.com.br",
            ImapSenhaProtegida = _protetor.Proteger("senha-app"), ImapUltimoUid = ultimoUid, ImapUidValidity = validade
        };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _loja = new Empresa { ClienteId = _rede.Id, Nome = "Loja", Cnpj = "11222333000262" };
        _db.Empresas.Add(_loja);
        await _db.SaveChangesAsync();
    }

    private LeituraEmailService Servico()
    {
        RegistroNotaSoPdf notasSoPdf = ServicosDeTeste.RegistroNotaSoPdf(_db);
        var ignorados = ServicosDeTeste.EmailsIgnoradosService(_db, notasSoPdf, TimeProvider.System, Options.Create(new LeituraEmailOptions()));
        return new LeituraEmailService(
            new Repositorio<Cliente>(_db),
            new Repositorio<Empresa>(_db),
            new Repositorio<FalhaProcessamento>(_db),
            _caixa,
            _protetor,
            ServicosDeTeste.RegistroNotaService(_db),
            new LeitorNFe(),
            notasSoPdf,
            ignorados,
            NullLogger<LeituraEmailService>.Instance);
    }

    private static EmailRecebido Email(long uid, string assunto, params ArquivoDoEmail[] arquivos)
    {
        var email = new EmailRecebido
        {
            Uid = uid, MensagemId = "msg-" + uid + "@fornecedor.com.br", De = "Alfa <nf@alfa.com.br>",
            Remetente = "nf@alfa.com.br", NomeRemetente = "Alfa", Assunto = assunto, Corpo = "Segue."
        };
        email.Arquivos.AddRange(arquivos);
        return email;
    }

    private static ArquivoDoEmail Arquivo(string nome, string tipo, byte[] conteudo)
    {
        return new ArquivoDoEmail { Nome = nome, Tipo = tipo, Conteudo = conteudo };
    }

    private static byte[] XmlExemplo()
    {
        return File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("nfe-92001.xml"));
    }

    private Task Ler()
    {
        return Servico().LerCaixaAsync(new CaixaParaLer(_rede, null), CancellationToken.None);
    }

    [Fact]
    public async Task Xml_vira_nota_e_o_uid_avanca()
    {
        await CadastrarRede();
        _caixa.Emails[11] = Email(11, "NF-e 92001", Arquivo("nfe.xml", "application/xml", XmlExemplo()));

        await Ler();

        NotaFiscal nota = _db.NotasFiscais.Single();
        Assert.Equal("92001", nota.Numero);
        Assert.Equal(_loja.Id, nota.EmpresaId);
        Assert.Contains("Alfa <nf@alfa.com.br> | NF-e 92001", nota.OrigemEmail);
        Assert.Equal(11, _db.Clientes.Single().ImapUltimoUid);
        Assert.Equal(new[] { "conectar imap.gmail.com:993", "entrar notas@rede.com.br senha-app", "abrir", "depois 10", "obter 11", "desconectar" }, _caixa.Pedidos);
        Assert.NotNull(_db.Clientes.Single().ImapUltimaConexaoEm);
    }

    [Fact]
    public async Task Xml_dentro_de_zip_e_xml_sem_nome_tambem_entram_e_o_repetido_nao_duplica()
    {
        await CadastrarRede();
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
        {
            ZipArchiveEntry entrada = zip.CreateEntry("pasta/nfe-92001.xml");
            using Stream fluxo = entrada.Open();
            fluxo.Write(XmlExemplo());
        }
        _caixa.Emails[11] = Email(11, "zip", Arquivo("notas.zip", "application/zip", memoria.ToArray()));
        _caixa.Emails[12] = Email(12, "inline", Arquivo("", "text/xml", XmlExemplo()));

        await Ler();

        Assert.Single(_db.NotasFiscais);
        Assert.Equal(12, _db.Clientes.Single().ImapUltimoUid);
    }

    [Fact]
    public async Task Pdf_de_nota_vira_pendencia_e_pdf_que_nao_e_nota_vai_para_ignorados()
    {
        await CadastrarRede();
        byte[] danfe = File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("danfe-chromium.pdf"));
        _caixa.Emails[11] = Email(11, "Nota fiscal", Arquivo("danfe.pdf", "application/pdf", danfe));
        _caixa.Emails[12] = Email(12, "Boleto", Arquivo("boleto.pdf", "application/pdf", new byte[] { 1, 2, 3 }));
        _caixa.Emails[13] = Email(13, "Fotos", Arquivo("foto.jpg", "image/jpeg", new byte[] { 1 }), Arquivo("", "image/png", new byte[] { 2 }));
        _caixa.Emails[14] = Email(14, "Sem anexo");

        await Ler();

        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, _db.NotasSoPdf.Single().Situacao);
        List<EmailIgnorado> ignorados = _db.EmailsIgnorados.OrderBy(e => e.Uid).ToList();
        Assert.Equal(2, ignorados.Count);
        Assert.Equal(12, ignorados[0].Uid);
        Assert.Equal("boleto.pdf", ignorados[0].PdfNome);
        Assert.Equal(13, ignorados[1].Uid);
        Assert.Equal(AnaliseEmail.MotivoAnexoSemNota, ignorados[1].Motivo);
        Assert.Equal("foto.jpg, (sem nome, image/png)", ignorados[1].Anexos);
        Assert.Equal(14, _db.Clientes.Single().ImapUltimoUid);
    }

    [Fact]
    public async Task Pdf_de_nota_que_ja_existe_vai_para_ignorados_com_a_nota()
    {
        await CadastrarRede();
        _caixa.Emails[11] = Email(11, "XML", Arquivo("nfe.xml", "application/xml", XmlExemplo()));
        byte[] danfe = File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("danfe-chromium.pdf"));
        _caixa.Emails[12] = Email(12, "Segunda via", Arquivo("danfe.pdf", "application/pdf", danfe));

        await Ler();

        EmailIgnorado ignorado = _db.EmailsIgnorados.Single();
        Assert.Equal("92001", ignorado.NumeroNota);
        Assert.Equal(_db.NotasFiscais.Single().Id, ignorado.NotaFiscalId);
        Assert.Empty(_db.NotasSoPdf);
    }

    [Fact]
    public async Task Primeira_leitura_busca_os_ultimos_dias_e_caixa_vazia_comeca_do_proximo()
    {
        await CadastrarRede(ultimoUid: null, validade: null);
        _caixa.Proximo = 501;

        await Ler();

        Assert.Contains("desde", _caixa.Pedidos);
        Cliente rede = _db.Clientes.Single();
        Assert.Equal(500, rede.ImapUltimoUid);
        Assert.Equal(7, rede.ImapUidValidity);
    }

    [Fact]
    public async Task Caixa_renumerada_recomeca_pelos_ultimos_dias()
    {
        await CadastrarRede(ultimoUid: 900, validade: 6);
        _caixa.Emails[3] = Email(3, "XML", Arquivo("nfe.xml", "application/xml", XmlExemplo()));

        await Ler();

        Assert.Contains("desde", _caixa.Pedidos);
        Assert.Single(_db.NotasFiscais);
        Assert.Equal(3, _db.Clientes.Single().ImapUltimoUid);
        Assert.Equal(7, _db.Clientes.Single().ImapUidValidity);
    }

    [Fact]
    public async Task Falha_ao_entrar_registra_a_falha_uma_vez_por_hora_e_marca_a_caixa()
    {
        await CadastrarRede();
        _caixa.ErroAoEntrar = new FalhaImapException(MotivoFalhaImap.Autenticacao, "senha recusada", null);

        await Assert.ThrowsAsync<FalhaImapException>(Ler);
        await Assert.ThrowsAsync<FalhaImapException>(Ler);

        FalhaProcessamento falha = _db.Falhas.Single();
        Assert.Contains("Falha ao entrar na caixa de e-mail (usuário ou senha): senha recusada", falha.Mensagem);
        Cliente rede = _db.Clientes.Single();
        Assert.NotNull(rede.ImapUltimaFalhaConexaoEm);
        Assert.Contains("senha recusada", rede.ImapUltimoErroConexao);
        Assert.Equal(10, rede.ImapUltimoUid);
    }

    [Fact]
    public async Task Email_com_erro_para_a_leitura_e_na_terceira_vez_e_pulado()
    {
        await CadastrarRede();
        _caixa.Emails[11] = Email(11, "quebrado");
        _caixa.Emails[12] = Email(12, "XML", Arquivo("nfe.xml", "application/xml", XmlExemplo()));
        _caixa.UidsComErro.Add(11);

        await Ler();
        await Ler();
        Assert.Equal(10, _db.Clientes.Single().ImapUltimoUid);
        Assert.Empty(_db.NotasFiscais);

        // Na 3ª falha o e-mail é pulado e a leitura segue, na mesma rodada, para o seguinte
        await Ler();
        Assert.Equal(3, _db.Falhas.Count(f => f.UidEmail == 11));
        Assert.Contains("ignorado depois de 3 tentativas", _db.Falhas.OrderBy(f => f.Id).Last().Mensagem);
        Assert.Single(_db.NotasFiscais);
        Assert.Equal(12, _db.Clientes.Single().ImapUltimoUid);
    }

    [Fact]
    public async Task Queda_de_conexao_no_meio_nao_conta_como_tentativa_do_email()
    {
        await CadastrarRede();
        _caixa.Emails[11] = Email(11, "quebrado");
        _caixa.UidsComErro.Add(11);
        _caixa.DerrubarNoErro = true;

        await Ler();

        FalhaProcessamento falha = _db.Falhas.Single();
        Assert.Null(falha.UidEmail);
        Assert.DoesNotContain("desconectar", _caixa.Pedidos);
        Assert.Equal(10, _db.Clientes.Single().ImapUltimoUid);
    }

    [Fact]
    public async Task Xml_que_nao_e_nfe_vira_falha_e_a_leitura_continua()
    {
        await CadastrarRede();
        _caixa.Emails[11] = Email(11, "outro xml", Arquivo("pedido.xml", "application/xml", Encoding.UTF8.GetBytes("<pedido/>")));

        await Ler();

        Assert.Equal(EtapaFalha.LeituraXml, _db.Falhas.Single().Etapa);
        Assert.Equal(11, _db.Clientes.Single().ImapUltimoUid);
    }

    [Fact]
    public async Task Ler_todas_pula_rede_com_teste_encerrado()
    {
        await CadastrarRede();
        var encerrada = new Cliente
        {
            Nome = "Encerrada", ImapHost = "imap.encerrada.com", ImapUsuario = "x", ImapSenhaProtegida = _protetor.Proteger("s"),
            Contrato = SituacaoContrato.EmTeste, TesteTerminaEm = DateTime.UtcNow.AddDays(-1)
        };
        _db.Clientes.Add(encerrada);
        await _db.SaveChangesAsync();

        await Servico().LerTodasAsync(CancellationToken.None);

        Assert.Contains("conectar imap.gmail.com:993", _caixa.Pedidos);
        Assert.DoesNotContain("conectar imap.encerrada.com:993", _caixa.Pedidos);
    }
}
