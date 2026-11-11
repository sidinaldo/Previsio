namespace Previsio.Api.Dtos;

/// <summary>
/// Dados do cliente para as telas. A senha do IMAP nunca sai da API.
/// </summary>
public class ClienteDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; }
    public bool ImapSsl { get; set; }
    public string ImapUsuario { get; set; } = "";

    // Aplicativo privado do Bling da rede. O secret nunca é devolvido, só se está cadastrado.
    public string? BlingClientId { get; set; }
    public bool BlingClientSecretCadastrado { get; set; }

    public bool Ativo { get; set; }
}
