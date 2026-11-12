using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Infra.Email;

/// <summary>
/// Uma conexão IMAP com o MailKit, com a caixa de entrada aberta só para leitura.
/// Converte cada mensagem em EmailRecebido, incluindo os arquivos enviados como "inline".
/// </summary>
public class SessaoImapMailKit : ISessaoImap
{
    private readonly ImapClient _imap = new ImapClient();
    private IMailFolder? _caixa;

    public bool Conectado
    {
        get { return _imap.IsConnected; }
    }

    public long ValidadeUids
    {
        get { return Caixa().UidValidity; }
    }

    public long? ProximoUid
    {
        get
        {
            UniqueId? proximo = Caixa().UidNext;
            if (proximo.HasValue)
            {
                return proximo.Value.Id;
            }
            return null;
        }
    }

    public Task ConectarAsync(string host, int porta, bool ssl, CancellationToken cancelamento)
    {
        // Mesma escolha do teste de conexão do cadastro
        SecureSocketOptions seguranca = SecureSocketOptions.StartTlsWhenAvailable;
        if (ssl)
        {
            seguranca = SecureSocketOptions.SslOnConnect;
        }
        return _imap.ConnectAsync(host, porta, seguranca, cancelamento);
    }

    public Task EntrarAsync(string usuario, string senha, CancellationToken cancelamento)
    {
        return _imap.AuthenticateAsync(usuario, senha, cancelamento);
    }

    public async Task AbrirCaixaDeEntradaAsync(CancellationToken cancelamento)
    {
        // Somente leitura: o Previsio não marca e-mail como lido nem altera a caixa
        _caixa = _imap.Inbox;
        await _caixa.OpenAsync(FolderAccess.ReadOnly, cancelamento);
    }

    public async Task<List<long>> ProcurarRecebidosDesdeAsync(DateTime desde, CancellationToken cancelamento)
    {
        IList<UniqueId> encontrados = await Caixa().SearchAsync(SearchQuery.DeliveredAfter(desde), cancelamento);
        return ParaLista(encontrados);
    }

    public async Task<List<long>> ProcurarDepoisDoUidAsync(long ultimoUid, CancellationToken cancelamento)
    {
        var novos = new UniqueIdRange(new UniqueId((uint)(ultimoUid + 1)), UniqueId.MaxValue);
        IList<UniqueId> encontrados = await Caixa().SearchAsync(SearchQuery.Uids(novos), cancelamento);
        return ParaLista(encontrados);
    }

    public async Task<EmailRecebido> ObterAsync(long uid, CancellationToken cancelamento)
    {
        MimeMessage mensagem = await Caixa().GetMessageAsync(new UniqueId((uint)uid), cancelamento);
        return Converter(uid, mensagem);
    }

    public Task DesconectarAsync(CancellationToken cancelamento)
    {
        return _imap.DisconnectAsync(true, cancelamento);
    }

    public void Dispose()
    {
        _imap.Dispose();
    }

    /// <summary>
    /// Mensagem do MimeKit para o formato da Aplicacao.
    /// </summary>
    public static EmailRecebido Converter(long uid, MimeMessage mensagem)
    {
        var email = new EmailRecebido();
        email.Uid = uid;
        email.MensagemId = mensagem.MessageId ?? "";
        email.De = mensagem.From.ToString();
        MailboxAddress? remetente = mensagem.From.Mailboxes.FirstOrDefault();
        if (remetente != null)
        {
            email.Remetente = remetente.Address ?? "";
            email.NomeRemetente = remetente.Name ?? "";
        }
        email.Assunto = mensagem.Subject ?? "";
        string corpo = mensagem.TextBody ?? "";
        if (corpo == "" && mensagem.HtmlBody != null)
        {
            corpo = Regex.Replace(mensagem.HtmlBody, "<[^>]+>", " ");
        }
        email.Corpo = corpo;
        if (mensagem.Date != DateTimeOffset.MinValue)
        {
            email.EnviadoEm = mensagem.Date.UtcDateTime;
        }

        foreach (MimePart parte in AnexosDoEmail.Listar(mensagem))
        {
            var arquivo = new ArquivoDoEmail();
            arquivo.Nome = parte.FileName ?? "";
            arquivo.Tipo = parte.ContentType.MimeType.ToLowerInvariant();
            if (parte.Content != null)
            {
                using var memoria = new MemoryStream();
                parte.Content.DecodeTo(memoria);
                arquivo.Conteudo = memoria.ToArray();
            }
            email.Arquivos.Add(arquivo);
        }
        return email;
    }

    private IMailFolder Caixa()
    {
        if (_caixa == null)
        {
            throw new InvalidOperationException("A caixa de entrada não foi aberta.");
        }
        return _caixa;
    }

    private static List<long> ParaLista(IList<UniqueId> encontrados)
    {
        var lista = new List<long>();
        foreach (UniqueId uid in encontrados)
        {
            lista.Add(uid.Id);
        }
        return lista;
    }
}
