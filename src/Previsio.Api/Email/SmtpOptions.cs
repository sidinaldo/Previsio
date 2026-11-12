namespace Previsio.Api.Email;

/// <summary>
/// Servidor que envia os e-mails do Previsio (seção "Smtp" do appsettings).
/// Serve para Gmail (senha de app), Brevo, Amazon SES ou qualquer SMTP.
/// A senha fica fora do Git (appsettings.Development.json ou variável de ambiente).
/// </summary>
public class SmtpOptions
{
    public const string Secao = "Smtp";

    public string Host { get; set; } = "";
    public int Porta { get; set; } = 587;

    // true: conexão segura desde o início (porta 465); false: STARTTLS quando o servidor oferece (porta 587)
    public bool Ssl { get; set; }

    public string Usuario { get; set; } = "";
    public string Senha { get; set; } = "";

    // Endereço que aparece como remetente (no Gmail, o mesmo da conta)
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "Previsio";

    public bool Configurado
    {
        get { return Host.Trim() != "" && Remetente.Trim() != ""; }
    }
}
