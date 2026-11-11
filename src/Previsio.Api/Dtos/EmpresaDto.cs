namespace Previsio.Api.Dtos;

public class EmpresaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public bool BlingConectado { get; set; }

    // Aplicativo privado do Bling desta empresa. O secret nunca é devolvido,
    // só se está cadastrado. Client ID vazio: usa o aplicativo global do Previsio.
    public string? BlingClientId { get; set; }
    public bool BlingClientSecretCadastrado { get; set; }

    // De onde vem o aplicativo do Bling em uso: "Empresa", "Rede" ou "Padrao"
    public string OrigemAplicativoBling { get; set; } = "";

    // Caixa de e-mail própria (opcional). A senha nunca é devolvida, só se está cadastrada.
    public bool EmailProprio { get; set; }
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; }
    public bool ImapSsl { get; set; }
    public string ImapUsuario { get; set; } = "";
    public bool ImapSenhaCadastrada { get; set; }
    public DateTime? UltimaSincronizacaoEm { get; set; }
    public bool Ativo { get; set; }
}
