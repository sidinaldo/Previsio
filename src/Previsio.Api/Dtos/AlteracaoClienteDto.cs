namespace Previsio.Api.Dtos;

/// <summary>
/// Só os campos preenchidos são alterados. A senha do IMAP só troca se vier preenchida.
/// </summary>
public class AlteracaoClienteDto
{
    public string? Nome { get; set; }
    public string? ImapHost { get; set; }
    public int? ImapPorta { get; set; }
    public bool? ImapSsl { get; set; }
    public string? ImapUsuario { get; set; }
    public string? ImapSenha { get; set; }

    // Desativar o cliente derruba na hora as sessões dos usuários dele
    public bool? Ativo { get; set; }
}
