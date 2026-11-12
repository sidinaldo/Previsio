using MailKit.Net.Imap;
using MailKit.Security;

namespace Previsio.Api.Servicos;

/// <summary>
/// Teste de conexão com o MailKit: conecta, entra e sai. Não abre pastas nem lê e-mails.
/// </summary>
public class ConexaoImapMailKit : IConexaoImap
{
    public async Task TestarAsync(string host, int porta, bool ssl, string usuario, string senha, CancellationToken cancelamento)
    {
        using var imap = new ImapClient();

        // Mesma escolha do job de leitura, para o teste valer o que o job vai fazer
        SecureSocketOptions seguranca = SecureSocketOptions.StartTlsWhenAvailable;
        if (ssl)
        {
            seguranca = SecureSocketOptions.SslOnConnect;
        }

        await imap.ConnectAsync(host, porta, seguranca, cancelamento);
        await imap.AuthenticateAsync(usuario, senha, cancelamento);
        await imap.DisconnectAsync(true, cancelamento);
    }
}
