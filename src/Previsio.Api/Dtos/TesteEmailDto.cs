namespace Previsio.Api.Dtos;

/// <summary>
/// POST /api/email/testar: dados da caixa digitados na tela, ainda não gravados.
/// Com a senha vazia e o ClienteId preenchido, usa a senha já gravada da rede.
/// </summary>
public class TesteEmailDto
{
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string? ImapSenha { get; set; }
    public int? ClienteId { get; set; }
}
