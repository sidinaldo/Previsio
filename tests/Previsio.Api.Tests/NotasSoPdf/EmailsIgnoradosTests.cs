using Microsoft.Extensions.Options;
using Previsio.Api.Dtos;
using Previsio.Api.EmailsIgnorados;
using Previsio.Api.NotasSoPdf;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Api.Tests.Sefaz;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;

namespace Previsio.Api.Tests.NotasSoPdf;

public class EmailsIgnoradosTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    // 11/11/2026 12h UTC
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private Cliente _rede = null!;
    private Empresa _loja = null!;

    private async Task CadastrarRede()
    {
        _rede = new Cliente { Nome = "Rede Auto Center" };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _loja = new Empresa { ClienteId = _rede.Id, Nome = "Auto Center Centro", Cnpj = "11222333000262" };
        _db.Empresas.Add(_loja);
        await _db.SaveChangesAsync();
    }

    private EmailsIgnoradosService Servico(int diasGuardar = 30)
    {
        var opcoes = new LeituraEmailOptions { DiasGuardarIgnorados = diasGuardar };
        return new EmailsIgnoradosService(_db, new RegistroNotaSoPdf(_db), _relogio, Options.Create(opcoes));
    }

    private static EmailSemXml Email(string assunto, string nomePdf, byte[] pdf)
    {
        var email = new EmailSemXml();
        email.MensagemId = Guid.NewGuid() + "@fornecedor.com.br";
        email.Remetente = "faturamento@alfa.com.br";
        email.Assunto = assunto;
        email.Pdfs.Add(new PdfDoEmail { Nome = nomePdf, Conteudo = pdf });
        return email;
    }

    private static EmailParaRegistrar ParaRegistrar(int clienteId, string mensagemId, string motivo, byte[]? pdf)
    {
        return new EmailParaRegistrar
        {
            ClienteId = clienteId, Uid = 77, MensagemId = mensagemId, Remetente = "faturamento@alfa.com.br",
            NomeRemetente = "Alfa", Assunto = "Documento", Anexos = new List<string> { "arquivo.pdf" }, Motivo = motivo,
            PdfNome = pdf == null ? null : "arquivo.pdf", Pdf = pdf
        };
    }

    // ---------- Motivo na análise do PDF ----------

    [Fact]
    public async Task Analise_diz_por_que_o_pdf_foi_ignorado_e_reconhece_o_que_ja_existe()
    {
        await CadastrarRede();
        var registro = new RegistroNotaSoPdf(_db);
        byte[] semTexto = new byte[] { 1, 2, 3 };

        ResultadoNotaSoPdf boleto = await registro.AnalisarAsync(_rede.Id, null, Email("Boleto de novembro", "boleto.pdf", semTexto), false, CancellationToken.None);
        Assert.Equal(0, boleto.Registradas);
        Assert.Equal(RegistroNotaSoPdf.MotivoNaoPareceNota, boleto.MotivoIgnorado);
        Assert.Equal("boleto.pdf", boleto.Pdf!.Nome);

        ResultadoNotaSoPdf vazio = await registro.AnalisarAsync(_rede.Id, null, Email("NF 1", "nf.pdf", Array.Empty<byte>()), false, CancellationToken.None);
        Assert.Equal(RegistroNotaSoPdf.MotivoPdfInvalido, vazio.MotivoIgnorado);
        Assert.Null(vazio.Pdf);

        EmailSemXml danfe = Email("NF-e 92001", "danfe.pdf", File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("danfe-chromium.pdf")));
        ResultadoNotaSoPdf primeira = await registro.AnalisarAsync(_rede.Id, null, danfe, false, CancellationToken.None);
        Assert.Equal(1, primeira.Registradas);
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, primeira.Situacao);
        Assert.Null(primeira.MotivoIgnorado);

        // Mesmo e-mail de novo e outra mensagem com a mesma nota: não é "ignorado", já existe
        ResultadoNotaSoPdf mesmaMensagem = await registro.AnalisarAsync(_rede.Id, null, danfe, false, CancellationToken.None);
        Assert.True(mesmaMensagem.JaConhecido);
        Assert.Null(mesmaMensagem.MotivoIgnorado);
        danfe.MensagemId = "outra@fornecedor.com.br";
        ResultadoNotaSoPdf mesmaNota = await registro.AnalisarAsync(_rede.Id, null, danfe, false, CancellationToken.None);
        Assert.True(mesmaNota.JaConhecido);

        // Forçado (o admin pediu): o PDF sem chave vira pendência "Sem chave"
        ResultadoNotaSoPdf forcado = await registro.AnalisarAsync(_rede.Id, null, Email("Documento", "doc.pdf", semTexto), true, CancellationToken.None);
        Assert.Equal(1, forcado.Registradas);
        Assert.Equal(SituacaoNotaSoPdf.SemChave, forcado.Situacao);
    }

    // ---------- Registro, lista e limpeza ----------

    [Fact]
    public async Task Registra_sem_duplicar_lista_por_rede_e_apaga_os_antigos()
    {
        await CadastrarRede();
        EmailsIgnoradosService servico = Servico(30);
        await servico.RegistrarAsync(ParaRegistrar(_rede.Id, "a@x", RegistroNotaSoPdf.MotivoNaoPareceNota, new byte[] { 1 }), CancellationToken.None);
        await _db.SaveChangesAsync();
        await servico.RegistrarAsync(ParaRegistrar(_rede.Id, "a@x", RegistroNotaSoPdf.MotivoNaoPareceNota, new byte[] { 1 }), CancellationToken.None);
        EmailParaRegistrar daLoja = ParaRegistrar(_rede.Id, "", "Os anexos não são XML, ZIP com XML nem PDF.", null);
        daLoja.EmpresaCaixaId = _loja.Id;
        await servico.RegistrarAsync(daLoja, CancellationToken.None);
        await _db.SaveChangesAsync();

        List<EmailIgnoradoDto> lista = await servico.ListarAsync(_rede.Id, 7, CancellationToken.None);
        Assert.Equal(2, lista.Count);
        EmailIgnoradoDto comPdf = lista.Single(e => e.TemPdf);
        Assert.Equal("Caixa da rede", comPdf.Caixa);
        Assert.Equal("Alfa <faturamento@alfa.com.br>", comPdf.Remetente);
        Assert.Equal("Rede Auto Center", comPdf.ClienteNome);
        Assert.Equal("Auto Center Centro", lista.Single(e => !e.TemPdf).Caixa);
        Assert.Empty(await servico.ListarAsync(_rede.Id + 1, 7, CancellationToken.None));

        // Um registro de 40 dias atrás sai na limpeza (prazo de 30)
        _db.EmailsIgnorados.First().LidoEm = _relogio.Agora.UtcDateTime.AddDays(-40);
        await _db.SaveChangesAsync();
        Assert.Equal(1, await servico.LimparAntigosAsync(CancellationToken.None));
        Assert.Single(_db.EmailsIgnorados);
    }

    // ---------- Tratar como nota em PDF ----------

    [Fact]
    public async Task Tratar_como_nota_cria_a_pendencia_uma_vez_so()
    {
        await CadastrarRede();
        EmailsIgnoradosService servico = Servico();
        await servico.RegistrarAsync(ParaRegistrar(_rede.Id, "b@x", RegistroNotaSoPdf.MotivoNaoPareceNota, new byte[] { 1, 2, 3 }), CancellationToken.None);
        await servico.RegistrarAsync(ParaRegistrar(_rede.Id, "c@x", "Os anexos não são XML, ZIP com XML nem PDF.", null), CancellationToken.None);
        await _db.SaveChangesAsync();
        int comPdf = _db.EmailsIgnorados.Single(e => e.MensagemId == "b@x").Id;
        int semPdf = _db.EmailsIgnorados.Single(e => e.MensagemId == "c@x").Id;

        Resultado<TratamentoEmailIgnoradoDto> tratado = await servico.TratarComoNotaAsync(comPdf, "admin@softio", CancellationToken.None);
        Assert.True(tratado.Sucesso, tratado.Mensagem);
        Assert.Contains("sem chave", tratado.Valor!.Mensagem);
        Assert.Equal("admin@softio", tratado.Valor.Email.TratadoPor);
        NotaSoPdf pendencia = _db.NotasSoPdf.Single();
        Assert.Equal(SituacaoNotaSoPdf.SemChave, pendencia.Situacao);
        Assert.Equal("b@x", pendencia.MensagemId);

        Assert.Equal(TipoResultado.Conflito, (await servico.TratarComoNotaAsync(comPdf, "admin", CancellationToken.None)).Tipo);
        Assert.Equal(TipoResultado.Invalido, (await servico.TratarComoNotaAsync(semPdf, "admin", CancellationToken.None)).Tipo);
        Assert.Equal(TipoResultado.NaoEncontrado, (await servico.TratarComoNotaAsync(999, "admin", CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task Tratar_como_nota_com_a_chave_no_pdf_vai_para_a_sefaz()
    {
        await CadastrarRede();
        EmailsIgnoradosService servico = Servico();
        byte[] danfe = File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("danfe-chromium.pdf"));
        await servico.RegistrarAsync(ParaRegistrar(_rede.Id, "d@x", RegistroNotaSoPdf.MotivoNaoPareceNota, danfe), CancellationToken.None);
        await _db.SaveChangesAsync();

        Resultado<TratamentoEmailIgnoradoDto> tratado = await servico.TratarComoNotaAsync(_db.EmailsIgnorados.Single().Id, "admin", CancellationToken.None);

        Assert.True(tratado.Sucesso, tratado.Mensagem);
        Assert.Contains("SEFAZ", tratado.Valor!.Mensagem);
        Assert.Equal(SituacaoNotaSoPdf.BuscandoNaSefaz, _db.NotasSoPdf.Single().Situacao);
    }

    // ---------- Nota que já estava no Previsio ----------

    [Fact]
    public async Task Pdf_de_nota_que_ja_existe_diz_qual_e_nao_deixa_tratar()
    {
        await CadastrarRede();
        byte[] danfe = File.ReadAllBytes(ChaveETextoPdfTests.Exemplo("danfe-chromium.pdf"));
        var registro = new RegistroNotaSoPdf(_db);

        // Primeiro só a pendência de outro e-mail
        await registro.AnalisarAsync(_rede.Id, null, Email("NF-e 92001", "danfe.pdf", danfe), false, CancellationToken.None);
        ResultadoNotaSoPdf pendente = await registro.AnalisarAsync(_rede.Id, null, Email("NF-e 92001 de novo", "danfe.pdf", danfe), false, CancellationToken.None);
        Assert.True(pendente.JaConhecido);
        Assert.Contains("já está pendente", pendente.MotivoJaExiste);
        Assert.Equal("92001", pendente.NumeroNotaExistente);
        Assert.Null(pendente.NotaFiscalExistenteId);

        // Depois o XML chega: o PDF de um terceiro e-mail aponta para a nota
        var nota = new NotaFiscal
        {
            ClienteId = _rede.Id, Numero = "92001", ChaveAcesso = "31261171295348000100550040000920011289748350",
            NomeFornecedor = "Fornecedor", RecebidaEm = new DateTime(2026, 11, 10, 13, 30, 0, DateTimeKind.Utc)
        };
        _db.NotasFiscais.Add(nota);
        await _db.SaveChangesAsync();
        ResultadoNotaSoPdf existente = await registro.AnalisarAsync(_rede.Id, null, Email("Segunda via", "danfe.pdf", danfe), false, CancellationToken.None);
        Assert.Equal(0, existente.Registradas);
        Assert.Null(existente.MotivoIgnorado);
        Assert.Equal(nota.Id, existente.NotaFiscalExistenteId);
        Assert.Equal("A nota deste PDF já está no Previsio (NF 92001, recebida em 10/11/2026 10:30). Nada foi criado.", existente.MotivoJaExiste);
        Assert.NotNull(existente.Pdf);

        // Na lista: mostra a nota e não oferece "Tratar como nota"
        EmailsIgnoradosService servico = Servico();
        EmailParaRegistrar email = ParaRegistrar(_rede.Id, "e@x", existente.MotivoJaExiste!, danfe);
        email.NotaFiscalId = existente.NotaFiscalExistenteId;
        email.NumeroNota = existente.NumeroNotaExistente;
        await servico.RegistrarAsync(email, CancellationToken.None);
        await _db.SaveChangesAsync();
        EmailIgnoradoDto item = (await servico.ListarAsync(_rede.Id, 7, CancellationToken.None)).Single();
        Assert.Equal("92001", item.NumeroNota);
        Assert.Equal(nota.Id, item.NotaFiscalId);
        Assert.True(item.TemPdf);
        Assert.False(item.PodeTratar);
        Assert.Equal(TipoResultado.Invalido, (await servico.TratarComoNotaAsync(item.Id, "admin", CancellationToken.None)).Tipo);
    }
}
