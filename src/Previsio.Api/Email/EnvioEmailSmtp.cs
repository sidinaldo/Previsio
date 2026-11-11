using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Previsio.Api.Email;

/// <summary>
/// Envio pelo servidor SMTP configurado em "Smtp", com o MailKit.
/// </summary>
public class EnvioEmailSmtp : IEnvioEmail
{
    private readonly SmtpOptions _opcoes;

    public EnvioEmailSmtp(IOptions<SmtpOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public bool Configurado
    {
        get { return _opcoes.Configurado; }
    }

    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento)
    {
        if (!_opcoes.Configurado)
        {
            throw new InvalidOperationException("Envio de e-mail não configurado: preencha a seção Smtp do appsettings.");
        }

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_opcoes.NomeRemetente, _opcoes.Remetente));
        email.To.Add(new MailboxAddress(mensagem.NomePara, mensagem.Para));
        email.Subject = mensagem.Assunto;

        var corpo = new BodyBuilder();
        corpo.HtmlBody = mensagem.Html;
        corpo.TextBody = mensagem.Texto;
        email.Body = corpo.ToMessageBody();

        SecureSocketOptions seguranca = SecureSocketOptions.StartTlsWhenAvailable;
        if (_opcoes.Ssl)
        {
            seguranca = SecureSocketOptions.SslOnConnect;
        }

        using var smtp = new SmtpClient();
        smtp.Timeout = 20000;
        await smtp.ConnectAsync(_opcoes.Host, _opcoes.Porta, seguranca, cancelamento);
        if (_opcoes.Usuario != "")
        {
            await smtp.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha, cancelamento);
        }
        await smtp.SendAsync(email, cancelamento);
        await smtp.DisconnectAsync(true, cancelamento);
    }
}
