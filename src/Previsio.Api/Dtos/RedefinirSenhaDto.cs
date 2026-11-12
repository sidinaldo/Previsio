namespace Previsio.Api.Dtos;

/// <summary>
/// Token que veio no link do e-mail e a senha nova.
/// </summary>
public class RedefinirSenhaDto
{
    public string Token { get; set; } = "";
    public string NovaSenha { get; set; } = "";
}
