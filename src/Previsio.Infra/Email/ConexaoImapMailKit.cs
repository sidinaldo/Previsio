using System.Net.Sockets;
using MailKit.Net.Imap;
using MailKit.Security;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Infra.Email;

/// <summary>
/// Teste de conexão com o MailKit: conecta, entra e sai. Não abre pastas nem lê e-mails.
/// As falhas conhecidas viram FalhaImapException com o motivo.
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

        try
        {
            await imap.ConnectAsync(host, porta, seguranca, cancelamento);
            await imap.AuthenticateAsync(usuario, senha, cancelamento);
            await imap.DisconnectAsync(true, cancelamento);
        }
        catch (AuthenticationException erro)
        {
            throw new FalhaImapException(MotivoFalhaImap.Autenticacao, erro.Message, erro);
        }
        catch (SslHandshakeException erro)
        {
            throw new FalhaImapException(MotivoFalhaImap.Ssl, erro.Message, erro);
        }
        catch (SocketException erro)
        {
            throw new FalhaImapException(MotivoFalhaImap.Rede, erro.Message, erro);
        }
    }
}
