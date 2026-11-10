using System.Net.Mail;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Regras de e-mail e senha usadas no cadastro, na troca de senha e no login.
/// </summary>
public static class RegrasUsuario
{
    public const int TamanhoMaximoEmail = 200;
    public const int TamanhoMinimoSenha = 8;

    // Limite alto o bastante para frases-senha e baixo o bastante para não pesar no hash
    public const int TamanhoMaximoSenha = 128;

    public static string NormalizarEmail(string? email)
    {
        return (email ?? "").Trim().ToLowerInvariant();
    }

    public static bool EmailValido(string emailNormalizado)
    {
        if (emailNormalizado.Length == 0 || emailNormalizado.Length > TamanhoMaximoEmail)
        {
            return false;
        }

        MailAddress? endereco;
        if (!MailAddress.TryCreate(emailNormalizado, out endereco))
        {
            return false;
        }

        // Recusa formatos como "Nome <email@dominio>"
        return endereco.Address == emailNormalizado;
    }

    /// <summary>
    /// Retorna a mensagem de erro, ou null quando a senha é aceita.
    /// </summary>
    public static string? ValidarSenha(string? senha)
    {
        if (senha == null || senha.Length < TamanhoMinimoSenha)
        {
            return "A senha precisa ter pelo menos " + TamanhoMinimoSenha + " caracteres.";
        }
        if (senha.Length > TamanhoMaximoSenha)
        {
            return "A senha pode ter no máximo " + TamanhoMaximoSenha + " caracteres.";
        }
        return null;
    }
}
