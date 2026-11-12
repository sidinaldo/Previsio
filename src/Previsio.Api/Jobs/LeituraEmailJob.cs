using System.IO.Compression;
using System.Text;
using Hangfire;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Previsio.Api.Contratos;
using Previsio.Api.NotasSoPdf;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;

namespace Previsio.Api.Jobs;

/// <summary>
/// Lê a caixa de e-mail de cada rede (cliente) e as caixas próprias das empresas que tiverem,
/// extrai os XMLs de NF-e (inclusive dentro de .zip) e registra as notas novas.
/// E-mail sem XML mas com a nota em PDF vira uma "nota só em PDF", para buscar o XML na SEFAZ.
/// </summary>
public class LeituraEmailJob
{
    private const int MaximoMensagensPorExecucao = 200;

    // A leitura roda a cada 10 minutos: a mesma falha de conexão só é registrada
    // de novo depois desse intervalo, para não encher a tela com o mesmo erro
    private static readonly TimeSpan IntervaloFalhaRepetida = TimeSpan.FromHours(1);

    // Motivo da última falha de conexão guardado na caixa (para o aviso)
    private const int TamanhoMaximoErroConexao = 500;


    private readonly PrevisioDbContext _db;
    private readonly ProtetorSegredos _protetor;
    private readonly RegistroNotaService _registro;
    private readonly LeitorNFe _leitor;
    private readonly RegistroNotaSoPdf _notasSoPdf;
    private readonly ILogger<LeituraEmailJob> _logger;

    public LeituraEmailJob(
        PrevisioDbContext db,
        ProtetorSegredos protetor,
        RegistroNotaService registro,
        LeitorNFe leitor,
        RegistroNotaSoPdf notasSoPdf,
        ILogger<LeituraEmailJob> logger)
    {
        _db = db;
        _protetor = protetor;
        _registro = registro;
        _leitor = leitor;
        _notasSoPdf = notasSoPdf;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecutarAsync(CancellationToken cancelamento)
    {
        List<Cliente> clientes = await _db.Clientes
            .Include(c => c.Empresas)
            .Where(PeriodoTeste.Processa(DateTime.UtcNow))
            .ToListAsync(cancelamento);

        foreach (Cliente cliente in clientes)
        {
            // Caixa da rede e caixas próprias das empresas
            foreach (CaixaParaLer caixa in CaixasDeEmail.Montar(cliente))
            {
                try
                {
                    await LerCaixaAsync(caixa, cancelamento);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception erro)
                {
                    // Uma caixa com problema não pode travar as demais
                    _logger.LogError(erro, "Erro ao ler a caixa de e-mail da {Caixa} ({Endereco})",
                        caixa.Descricao, FabricaFalha.EnderecoImap(caixa.Dados));
                }
            }
        }
    }

    private async Task LerCaixaAsync(CaixaParaLer caixaParaLer, CancellationToken cancelamento)
    {
        Cliente cliente = caixaParaLer.Rede;
        ICaixaEmail dados = caixaParaLer.Dados;

        // Uma falha em outra caixa limpa o ChangeTracker; volta a acompanhar esta
        // para o último UID lido ser gravado
        AcompanharDonoDaCaixa(caixaParaLer);

        using var imap = new ImapClient();
        IMailFolder caixa;
        IList<UniqueId> encontrados;

        // Passo atual, para a falha dizer exatamente onde parou
        string passo = "abrir a senha do e-mail gravada no Previsio";
        try
        {
            string senha = _protetor.Revelar(dados.ImapSenhaProtegida);

            SecureSocketOptions seguranca = SecureSocketOptions.StartTlsWhenAvailable;
            if (dados.ImapSsl)
            {
                seguranca = SecureSocketOptions.SslOnConnect;
            }

            passo = "conectar ao servidor de e-mail";
            await imap.ConnectAsync(dados.ImapHost, dados.ImapPorta, seguranca, cancelamento);

            passo = "entrar na caixa de e-mail (usuário ou senha)";
            await imap.AuthenticateAsync(dados.ImapUsuario, senha, cancelamento);

            // Somente leitura: o Previsio não marca e-mail como lido nem altera a caixa
            passo = "abrir a caixa de entrada";
            caixa = imap.Inbox;
            await caixa.OpenAsync(FolderAccess.ReadOnly, cancelamento);

            // Entrou: a caixa deixa de aparecer como "com erro" nos avisos
            dados.ImapUltimaConexaoEm = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancelamento);

            passo = "procurar e-mails novos";
            long validadeAtual = caixa.UidValidity;
            if (ControleUidEmail.PrecisaRecomecar(dados.ImapUidValidity, dados.ImapUltimoUid, validadeAtual))
            {
                // Primeira leitura ou o servidor renumerou a caixa: lê os últimos dias.
                // Notas já registradas são reconhecidas pela chave de acesso e não duplicam.
                dados.ImapUidValidity = validadeAtual;
                dados.ImapUltimoUid = null;
                await _db.SaveChangesAsync(cancelamento);

                DateTime desde = DateTime.UtcNow.Date.AddDays(-ControleUidEmail.DiasPrimeiraLeitura);
                encontrados = await caixa.SearchAsync(SearchQuery.DeliveredAfter(desde), cancelamento);

                if (encontrados.Count == 0 && caixa.UidNext.HasValue)
                {
                    // Nada nos últimos dias: começa a acompanhar a partir do próximo e-mail
                    dados.ImapUltimoUid = caixa.UidNext.Value.Id - 1;
                    await _db.SaveChangesAsync(cancelamento);
                }
            }
            else
            {
                var novos = new UniqueIdRange(new UniqueId((uint)(dados.ImapUltimoUid!.Value + 1)), UniqueId.MaxValue);
                encontrados = await caixa.SearchAsync(SearchQuery.Uids(novos), cancelamento);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            await RegistrarFalhaDeConexaoAsync(caixaParaLer, "Falha ao " + passo + ": " + erro.Message, cancelamento);
            throw;
        }

        // Do mais antigo para o mais novo, para o último UID gravado sempre avançar
        List<UniqueId> paraLer = ControleUidEmail.ProximosParaLer(
            encontrados, dados.ImapUltimoUid, MaximoMensagensPorExecucao);

        int registradas = 0;
        int duplicadas = 0;
        int processadas = 0;
        int soPdf = 0;

        foreach (UniqueId uid in paraLer)
        {
            string? origem = null;
            try
            {
                MimeMessage mensagem = await caixa.GetMessageAsync(uid, cancelamento);
                origem = DescreverOrigem(mensagem);

                List<AnexoXml> xmls = ExtrairXmls(mensagem);
                if (xmls.Count == 0)
                {
                    // Sem XML: pode ser a nota só em PDF (DANFE)
                    EmailSemXml? semXml = EmailComPdf(mensagem, origem);
                    if (semXml != null)
                    {
                        soPdf += await _notasSoPdf.RegistrarAsync(cliente.Id, caixaParaLer.EmpresaId, semXml, cancelamento);
                    }
                }

                foreach (AnexoXml anexo in xmls)
                {
                    NotaFiscalXml? nota = _leitor.Ler(anexo.Conteudo);
                    if (nota == null)
                    {
                        _db.Falhas.Add(FabricaFalha.DoXml(cliente.Id, caixaParaLer.EmpresaId, dados, anexo.Nome, origem));
                        await _db.SaveChangesAsync(cancelamento);
                        continue;
                    }

                    ResultadoRegistro resultado = await _registro.RegistrarAsync(cliente.Id, nota, origem, caixaParaLer.EmpresaId, cancelamento);
                    if (resultado == ResultadoRegistro.Registrada)
                    {
                        registradas++;
                    }
                    else
                    {
                        duplicadas++;
                    }
                }

                // Só avança depois de registrar, para não perder nota se der erro no meio
                dados.ImapUltimoUid = uid.Id;
                await _db.SaveChangesAsync(cancelamento);
                processadas++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception erro)
            {
                _logger.LogError(erro, "Erro ao ler o e-mail {Uid} da {Caixa}", uid, caixaParaLer.Descricao);
                bool continuar = await TratarFalhaDoEmailAsync(caixaParaLer, uid, origem, erro, imap.IsConnected, cancelamento);
                if (!continuar)
                {
                    break;
                }
            }
        }

        if (!imap.IsConnected)
        {
            _logger.LogWarning("Conexão com a caixa de e-mail da {Caixa} caiu durante a leitura", caixaParaLer.Descricao);
            return;
        }

        await imap.DisconnectAsync(true, cancelamento);

        _logger.LogInformation(
            "Caixa da {Caixa}: {Processadas} e-mails lidos, {Registradas} notas novas, {Duplicadas} já existentes, {SoPdf} só em PDF",
            caixaParaLer.Descricao, processadas, registradas, duplicadas, soPdf);
    }

    /// <summary>
    /// Registra a falha de um e-mail e decide se a leitura continua.
    /// Sem conexão ou com menos de 3 tentativas: para aqui e o mesmo e-mail é tentado na próxima execução.
    /// Na 3ª falha do mesmo e-mail: pula ele, para não travar os seguintes.
    /// </summary>
    private async Task<bool> TratarFalhaDoEmailAsync(
        CaixaParaLer caixaParaLer, UniqueId uid, string? origem, Exception erro, bool conectado, CancellationToken cancelamento)
    {
        Cliente cliente = caixaParaLer.Rede;
        ICaixaEmail dados = caixaParaLer.Dados;
        int? empresaId = caixaParaLer.EmpresaId;

        // Descarta o que ficou pela metade (nota, parcelas) antes de gravar a falha
        _db.ChangeTracker.Clear();
        AcompanharDonoDaCaixa(caixaParaLer);

        string mensagem = "Falha ao ler o e-mail (UID " + uid.Id + "): " + erro.Message;

        if (!conectado)
        {
            // Queda de conexão não é culpa do e-mail: não conta como tentativa dele
            _db.Falhas.Add(FabricaFalha.DoEmail(cliente.Id, empresaId, dados, mensagem, origem));
            await _db.SaveChangesAsync(cancelamento);
            return false;
        }

        // UIDs são por caixa: conta só as falhas desta caixa (rede ou empresa)
        long uidEmail = uid.Id;
        int falhasAnteriores = await _db.Falhas.CountAsync(f =>
            f.ClienteId == cliente.Id
            && f.EmpresaId == empresaId
            && f.Etapa == EtapaFalha.LeituraEmail
            && f.UidEmail == uidEmail, cancelamento);

        bool desistir = falhasAnteriores + 1 >= ControleUidEmail.MaximoTentativasPorEmail;
        if (desistir)
        {
            mensagem = mensagem + " O e-mail foi ignorado depois de "
                + ControleUidEmail.MaximoTentativasPorEmail + " tentativas; confira-o na caixa de e-mail.";
        }

        FalhaProcessamento falha = FabricaFalha.DoEmail(cliente.Id, empresaId, dados, mensagem, origem);
        falha.UidEmail = uidEmail;
        _db.Falhas.Add(falha);

        if (desistir)
        {
            dados.ImapUltimoUid = uidEmail;
        }

        await _db.SaveChangesAsync(cancelamento);
        return desistir;
    }

    /// <summary>
    /// Grava a falha de conexão com a caixa de e-mail, exceto se a mesma mensagem
    /// já foi registrada para essa caixa há menos de uma hora.
    /// </summary>
    private async Task RegistrarFalhaDeConexaoAsync(CaixaParaLer caixaParaLer, string mensagem, CancellationToken cancelamento)
    {
        int clienteId = caixaParaLer.Rede.Id;
        int? empresaId = caixaParaLer.EmpresaId;
        try
        {
            FalhaProcessamento falha = FabricaFalha.DoEmail(clienteId, empresaId, caixaParaLer.Dados, mensagem, null);
            DateTime limite = DateTime.UtcNow.Subtract(IntervaloFalhaRepetida);

            bool jaRegistrada = await _db.Falhas.AnyAsync(f =>
                f.ClienteId == clienteId
                && f.EmpresaId == empresaId
                && f.Etapa == EtapaFalha.LeituraEmail
                && f.Mensagem == falha.Mensagem
                && f.OcorridaEm >= limite, cancelamento);

            _db.ChangeTracker.Clear();
            AcompanharDonoDaCaixa(caixaParaLer);

            // Marca a caixa como "com erro" (para os avisos) mesmo quando a falha repetida não é gravada de novo
            caixaParaLer.Dados.ImapUltimaFalhaConexaoEm = DateTime.UtcNow;
            caixaParaLer.Dados.ImapUltimoErroConexao = LimitarErro(mensagem);
            if (!jaRegistrada)
            {
                _db.Falhas.Add(falha);
            }
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (Exception erro)
        {
            // Falha ao gravar o log não pode esconder o erro original
            _logger.LogError(erro, "Não foi possível registrar a falha de e-mail da {Caixa}", caixaParaLer.Descricao);
        }
    }

    /// <summary>
    /// Garante que a rede (e a empresa, quando a caixa é dela) está sendo acompanhada pelo
    /// DbContext, para o último UID lido ser gravado depois de um ChangeTracker.Clear().
    /// </summary>
    private void AcompanharDonoDaCaixa(CaixaParaLer caixaParaLer)
    {
        if (_db.Entry(caixaParaLer.Rede).State == EntityState.Detached)
        {
            _db.Clientes.Attach(caixaParaLer.Rede);
        }
        if (caixaParaLer.Empresa != null && _db.Entry(caixaParaLer.Empresa).State == EntityState.Detached)
        {
            _db.Empresas.Attach(caixaParaLer.Empresa);
        }
    }

    private static string LimitarErro(string mensagem)
    {
        if (mensagem.Length <= TamanhoMaximoErroConexao)
        {
            return mensagem;
        }
        return mensagem.Substring(0, TamanhoMaximoErroConexao);
    }

    private static string DescreverOrigem(MimeMessage mensagem)
    {
        string remetente = mensagem.From.ToString();
        string assunto = mensagem.Subject ?? "";
        return remetente + " | " + assunto;
    }

    /// <summary>
    /// Os PDFs anexados e o que o e-mail diz (remetente, assunto, corpo); nulo se não houver PDF.
    /// </summary>
    private static EmailSemXml? EmailComPdf(MimeMessage mensagem, string origem)
    {
        var email = new EmailSemXml();
        foreach (MimeEntity anexo in mensagem.Attachments)
        {
            MimePart? parte = anexo as MimePart;
            if (parte == null || parte.Content == null)
            {
                continue;
            }
            string nome = parte.FileName ?? "";
            bool ehPdf = nome.ToLowerInvariant().EndsWith(".pdf") || parte.ContentType.IsMimeType("application", "pdf");
            if (!ehPdf)
            {
                continue;
            }
            using var memoria = new MemoryStream();
            parte.Content.DecodeTo(memoria);
            var pdf = new PdfDoEmail();
            pdf.Nome = nome;
            pdf.Conteudo = memoria.ToArray();
            email.Pdfs.Add(pdf);
        }
        if (email.Pdfs.Count == 0)
        {
            return null;
        }

        MailboxAddress? remetente = mensagem.From.Mailboxes.FirstOrDefault();
        if (remetente != null)
        {
            email.Remetente = remetente.Address ?? "";
            email.NomeRemetente = remetente.Name ?? "";
        }
        email.MensagemId = mensagem.MessageId ?? "";
        email.Assunto = mensagem.Subject ?? "";
        email.Origem = origem;
        string corpo = mensagem.TextBody ?? "";
        if (corpo == "" && mensagem.HtmlBody != null)
        {
            corpo = Regex.Replace(mensagem.HtmlBody, "<[^>]+>", " ");
        }
        email.Corpo = corpo;
        return email;
    }

    private static List<AnexoXml> ExtrairXmls(MimeMessage mensagem)
    {
        var xmls = new List<AnexoXml>();

        foreach (MimeEntity anexo in mensagem.Attachments)
        {
            MimePart? parte = anexo as MimePart;
            if (parte == null || parte.Content == null)
            {
                continue;
            }

            string nomeArquivo = (parte.FileName ?? "").ToLowerInvariant();

            using var memoria = new MemoryStream();
            parte.Content.DecodeTo(memoria);
            byte[] bytes = memoria.ToArray();

            if (nomeArquivo.EndsWith(".xml"))
            {
                xmls.Add(NovoAnexo(parte.FileName ?? "", bytes));
            }
            else if (nomeArquivo.EndsWith(".zip"))
            {
                xmls.AddRange(ExtrairXmlsDoZip(parte.FileName ?? "", bytes));
            }
        }

        return xmls;
    }

    private static List<AnexoXml> ExtrairXmlsDoZip(string nomeZip, byte[] bytes)
    {
        var xmls = new List<AnexoXml>();
        try
        {
            using var memoria = new MemoryStream(bytes);
            using var zip = new ZipArchive(memoria, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entrada in zip.Entries)
            {
                if (!entrada.Name.ToLowerInvariant().EndsWith(".xml"))
                {
                    continue;
                }
                // Proteção contra arquivos gigantes dentro do zip
                if (entrada.Length > 5 * 1024 * 1024)
                {
                    continue;
                }

                using Stream fluxo = entrada.Open();
                using var destino = new MemoryStream();
                fluxo.CopyTo(destino);
                xmls.Add(NovoAnexo(nomeZip + "/" + entrada.FullName, destino.ToArray()));
            }
        }
        catch (InvalidDataException)
        {
            // Zip corrompido: ignora
        }
        return xmls;
    }

    private static AnexoXml NovoAnexo(string nome, byte[] bytes)
    {
        var anexo = new AnexoXml();
        anexo.Nome = nome;
        anexo.Conteudo = LerTexto(bytes);
        return anexo;
    }

    private static string LerTexto(byte[] bytes)
    {
        // XML de NF-e é UTF-8; remove BOM se houver
        string texto = Encoding.UTF8.GetString(bytes);
        return texto.TrimStart('﻿');
    }
}
