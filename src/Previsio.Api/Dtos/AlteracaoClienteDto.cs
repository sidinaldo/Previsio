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

    // Aplicativo privado do Bling da rede. Client ID vazio ("") remove o aplicativo da rede.
    // Secret vazio ou ausente mantém o atual. Trocar o Client ID desconecta as empresas
    // que usam o aplicativo da rede: os tokens pertencem ao aplicativo anterior.
    public string? BlingClientId { get; set; }
    public string? BlingClientSecret { get; set; }

    // Desativar o cliente derruba na hora as sessões dos usuários dele
    public bool? Ativo { get; set; }
}
