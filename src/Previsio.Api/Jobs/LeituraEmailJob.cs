using System.IO.Compression;
using System.Text;
using Hangfire;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;

namespace Previsio.Api.Jobs;

/// <summary>
/// Lê a caixa de e-mail dedicada de cada cliente, extrai os XMLs de NF-e
/// (inclusive dentro de .zip) e registra as notas novas.
/// </summary>
public class LeituraEmailJob
{
    private const int MaximoMensagensPorExecucao = 200;

    // A leitura roda a cada 10 minutos: a mesma falha de conexão só é registrada
    // de novo depois desse intervalo, para não encher a tela com o mesmo erro
    private static readonly TimeSpan IntervaloFalhaRepetida = TimeSpan.FromHours(1);

    private readonly PrevisioDbContext _db;
    private readonly ProtetorSegredos _protetor;
    private readonly RegistroNotaService _registro;
    private readonly LeitorNFe _leitor;
    private readonly ILogger<LeituraEmailJob> _logger;

    public LeituraEmailJob(
        PrevisioDbContext db,
        ProtetorSegredos protetor,
        RegistroNotaService registro,
        LeitorNFe leitor,
        ILogger<LeituraEmailJob> logger)
    {
        _db = db;
        _protetor = protetor;
        _registro = registro;
        _leitor = leitor;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecutarAsync(CancellationToken cancelamento)
    {
        List<Cliente> clientes = await _db.Clientes
            .Where(c => c.Ativo && c.ImapHost != "" && c.ImapUsuario != "")
            .ToListAsync(cancelamento);

        foreach (Cliente cliente in clientes)
        {
            try
            {
                await LerCaixaDoClienteAsync(cliente, cancelamento);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception erro)
            {
                // Um cliente com problema não pode travar os demais
                _logger.LogError(erro, "Erro ao ler a caixa de e-mail do cliente {ClienteId} ({Endereco})",
                    cliente.Id, FabricaFalha.EnderecoImap(cliente));
            }
        }
    }

    private async Task LerCaixaDoClienteAsync(Cliente cliente, CancellationToken cancelamento)
    {
        // Uma falha em outro cliente limpa o ChangeTracker; volta a acompanhar este
        // para o último UID lido ser gravado
        if (_db.Entry(cliente).State == EntityState.Detached)
        {
            _db.Clientes.Attach(cliente);
        }

        using var imap = new ImapClient();
        IMailFolder caixa;
        IList<UniqueId> encontrados;

        // Passo atual, para a falha dizer exatamente onde parou
        string passo = "abrir a senha do e-mail gravada no Previsio";
        try
        {
            string senha = _protetor.Revelar(cliente.ImapSenhaProtegida);

            SecureSocketOptions seguranca = SecureSocketOptions.StartTlsWhenAvailable;
            if (cliente.ImapSsl)
            {
                seguranca = SecureSocketOptions.SslOnConnect;
            }

            passo = "conectar ao servidor de e-mail";
            await imap.ConnectAsync(cliente.ImapHost, cliente.ImapPorta, seguranca, cancelamento);

            passo = "entrar na caixa de e-mail (usuário ou senha)";
            await imap.AuthenticateAsync(cliente.ImapUsuario, senha, cancelamento);

            // Somente leitura: o Previsio não marca e-mail como lido nem altera a caixa
            passo = "abrir a caixa de entrada";
            caixa = imap.Inbox;
            await caixa.OpenAsync(FolderAccess.ReadOnly, cancelamento);

            passo = "procurar e-mails novos";
            long validadeAtual = caixa.UidValidity;
            if (ControleUidEmail.PrecisaRecomecar(cliente.ImapUidValidity, cliente.ImapUltimoUid, validadeAtual))
            {
                // Primeira leitura ou o servidor renumerou a caixa: lê os últimos dias.
                // Notas já registradas são reconhecidas pela chave de acesso e não duplicam.
                cliente.ImapUidValidity = validadeAtual;
                cliente.ImapUltimoUid = null;
                await _db.SaveChangesAsync(cancelamento);

                DateTime desde = DateTime.UtcNow.Date.AddDays(-ControleUidEmail.DiasPrimeiraLeitura);
                encontrados = await caixa.SearchAsync(SearchQuery.DeliveredAfter(desde), cancelamento);

                if (encontrados.Count == 0 && caixa.UidNext.HasValue)
                {
                    // Nada nos últimos dias: começa a acompanhar a partir do próximo e-mail
                    cliente.ImapUltimoUid = caixa.UidNext.Value.Id - 1;
                    await _db.SaveChangesAsync(cancelamento);
                }
            }
            else
            {
                var novos = new UniqueIdRange(new UniqueId((uint)(cliente.ImapUltimoUid!.Value + 1)), UniqueId.MaxValue);
                encontrados = await caixa.SearchAsync(SearchQuery.Uids(novos), cancelamento);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            await RegistrarFalhaDeConexaoAsync(cliente, "Falha ao " + passo + ": " + erro.Message, cancelamento);
            throw;
        }

        // Do mais antigo para o mais novo, para o último UID gravado sempre avançar
        List<UniqueId> paraLer = ControleUidEmail.ProximosParaLer(
            encontrados, cliente.ImapUltimoUid, MaximoMensagensPorExecucao);

        int registradas = 0;
        int duplicadas = 0;
        int processadas = 0;

        foreach (UniqueId uid in paraLer)
        {
            string? origem = null;
            try
            {
                MimeMessage mensagem = await caixa.GetMessageAsync(uid, cancelamento);
                origem = DescreverOrigem(mensagem);

                foreach (AnexoXml anexo in ExtrairXmls(mensagem))
                {
                    NotaFiscalXml? nota = _leitor.Ler(anexo.Conteudo);
                    if (nota == null)
                    {
                        _db.Falhas.Add(FabricaFalha.DoXml(cliente, anexo.Nome, origem));
                        await _db.SaveChangesAsync(cancelamento);
                        continue;
                    }

                    ResultadoRegistro resultado = await _registro.RegistrarAsync(cliente.Id, nota, origem, cancelamento);
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
                cliente.ImapUltimoUid = uid.Id;
                await _db.SaveChangesAsync(cancelamento);
                processadas++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception erro)
            {
                _logger.LogError(erro, "Erro ao ler o e-mail {Uid} do cliente {ClienteId}", uid, cliente.Id);
                bool continuar = await TratarFalhaDoEmailAsync(cliente, uid, origem, erro, imap.IsConnected, cancelamento);
                if (!continuar)
                {
                    break;
                }
            }
        }

        if (!imap.IsConnected)
        {
            _logger.LogWarning("Conexão com a caixa de e-mail do cliente {ClienteId} caiu durante a leitura", cliente.Id);
            return;
        }

        await imap.DisconnectAsync(true, cancelamento);

        _logger.LogInformation(
            "Cliente {ClienteId}: {Processadas} e-mails lidos, {Registradas} notas novas, {Duplicadas} já existentes",
            cliente.Id, processadas, registradas, duplicadas);
    }

    /// <summary>
    /// Registra a falha de um e-mail e decide se a leitura continua.
    /// Sem conexão ou com menos de 3 tentativas: para aqui e o mesmo e-mail é tentado na próxima execução.
    /// Na 3ª falha do mesmo e-mail: pula ele, para não travar os seguintes.
    /// </summary>
    private async Task<bool> TratarFalhaDoEmailAsync(
        Cliente cliente, UniqueId uid, string? origem, Exception erro, bool conectado, CancellationToken cancelamento)
    {
        // Descarta o que ficou pela metade (nota, parcelas) antes de gravar a falha
        _db.ChangeTracker.Clear();
        _db.Clientes.Attach(cliente);

        string mensagem = "Falha ao ler o e-mail (UID " + uid.Id + "): " + erro.Message;

        if (!conectado)
        {
            // Queda de conexão não é culpa do e-mail: não conta como tentativa dele
            _db.Falhas.Add(FabricaFalha.DoEmail(cliente, mensagem, origem));
            await _db.SaveChangesAsync(cancelamento);
            return false;
        }

        long uidEmail = uid.Id;
        int falhasAnteriores = await _db.Falhas.CountAsync(f =>
            f.ClienteId == cliente.Id
            && f.Etapa == EtapaFalha.LeituraEmail
            && f.UidEmail == uidEmail, cancelamento);

        bool desistir = falhasAnteriores + 1 >= ControleUidEmail.MaximoTentativasPorEmail;
        if (desistir)
        {
            mensagem = mensagem + " O e-mail foi ignorado depois de "
                + ControleUidEmail.MaximoTentativasPorEmail + " tentativas; confira-o na caixa de e-mail.";
        }

        FalhaProcessamento falha = FabricaFalha.DoEmail(cliente, mensagem, origem);
        falha.UidEmail = uidEmail;
        _db.Falhas.Add(falha);

        if (desistir)
        {
            cliente.ImapUltimoUid = uidEmail;
        }

        await _db.SaveChangesAsync(cancelamento);
        return desistir;
    }

    /// <summary>
    /// Grava a falha de conexão com a caixa de e-mail, exceto se a mesma mensagem
    /// já foi registrada para esse cliente há menos de uma hora.
    /// </summary>
    private async Task RegistrarFalhaDeConexaoAsync(Cliente cliente, string mensagem, CancellationToken cancelamento)
    {
        try
        {
            FalhaProcessamento falha = FabricaFalha.DoEmail(cliente, mensagem, null);
            DateTime limite = DateTime.UtcNow.Subtract(IntervaloFalhaRepetida);

            bool jaRegistrada = await _db.Falhas.AnyAsync(f =>
                f.ClienteId == cliente.Id
                && f.Etapa == EtapaFalha.LeituraEmail
                && f.Mensagem == falha.Mensagem
                && f.OcorridaEm >= limite, cancelamento);

            if (jaRegistrada)
            {
                return;
            }

            _db.ChangeTracker.Clear();
            _db.Falhas.Add(falha);
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (Exception erro)
        {
            // Falha ao gravar o log não pode esconder o erro original
            _logger.LogError(erro, "Não foi possível registrar a falha de e-mail do cliente {ClienteId}", cliente.Id);
        }
    }

    private static string DescreverOrigem(MimeMessage mensagem)
    {
        string remetente = mensagem.From.ToString();
        string assunto = mensagem.Subject ?? "";
        return remetente + " | " + assunto;
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
