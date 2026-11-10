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
            catch (Exception erro)
            {
                // Um cliente com problema não pode travar os demais
                _logger.LogError(erro, "Erro ao ler a caixa de e-mail do cliente {ClienteId}", cliente.Id);
            }
        }
    }

    private async Task LerCaixaDoClienteAsync(Cliente cliente, CancellationToken cancelamento)
    {
        string senha = _protetor.Revelar(cliente.ImapSenhaProtegida);

        using var imap = new ImapClient();
        SecureSocketOptions seguranca = SecureSocketOptions.StartTlsWhenAvailable;
        if (cliente.ImapSsl)
        {
            seguranca = SecureSocketOptions.SslOnConnect;
        }

        await imap.ConnectAsync(cliente.ImapHost, cliente.ImapPorta, seguranca, cancelamento);
        await imap.AuthenticateAsync(cliente.ImapUsuario, senha, cancelamento);

        IMailFolder caixa = imap.Inbox;
        await caixa.OpenAsync(FolderAccess.ReadWrite, cancelamento);

        IList<UniqueId> naoLidas = await caixa.SearchAsync(SearchQuery.NotSeen, cancelamento);
        int registradas = 0;
        int duplicadas = 0;
        int processadas = 0;

        foreach (UniqueId uid in naoLidas)
        {
            if (processadas >= MaximoMensagensPorExecucao)
            {
                break;
            }
            processadas++;

            MimeMessage mensagem = await caixa.GetMessageAsync(uid, cancelamento);
            string origem = DescreverOrigem(mensagem);

            foreach (string xml in ExtrairXmls(mensagem))
            {
                NotaFiscalXml? nota = _leitor.Ler(xml);
                if (nota == null)
                {
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

            // Marca como lida só depois de registrar, para não perder nota se der erro no meio
            await caixa.AddFlagsAsync(uid, MessageFlags.Seen, true, cancelamento);
        }

        await imap.DisconnectAsync(true, cancelamento);

        _logger.LogInformation(
            "Cliente {ClienteId}: {Mensagens} e-mails lidos, {Registradas} notas novas, {Duplicadas} já existentes",
            cliente.Id, processadas, registradas, duplicadas);
    }

    private static string DescreverOrigem(MimeMessage mensagem)
    {
        string remetente = mensagem.From.ToString();
        string assunto = mensagem.Subject ?? "";
        return remetente + " | " + assunto;
    }

    private static List<string> ExtrairXmls(MimeMessage mensagem)
    {
        var xmls = new List<string>();

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
                xmls.Add(LerTexto(bytes));
            }
            else if (nomeArquivo.EndsWith(".zip"))
            {
                xmls.AddRange(ExtrairXmlsDoZip(bytes));
            }
        }

        return xmls;
    }

    private static List<string> ExtrairXmlsDoZip(byte[] bytes)
    {
        var xmls = new List<string>();
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
                xmls.Add(LerTexto(destino.ToArray()));
            }
        }
        catch (InvalidDataException)
        {
            // Zip corrompido: ignora
        }
        return xmls;
    }

    private static string LerTexto(byte[] bytes)
    {
        // XML de NF-e é UTF-8; remove BOM se houver
        string texto = Encoding.UTF8.GetString(bytes);
        return texto.TrimStart('﻿');
    }
}
