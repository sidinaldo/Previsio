namespace Previsio.Core.Entidades;

/// <summary>
/// Cada CNPJ do cliente. Cada empresa tem a sua própria conta no Bling.
/// </summary>
public class Empresa : ICaixaEmail
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";

    // Caixa de e-mail própria da empresa (opcional). Vazia: as notas dela chegam pela caixa da rede.
    // Com caixa própria, as duas são lidas; a nota vai para a empresa pelo CNPJ do destinatário.
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string ImapSenhaProtegida { get; set; } = "";
    public long? ImapUidValidity { get; set; }
    public long? ImapUltimoUid { get; set; }

    // Aplicativo privado criado dentro da conta Bling desta empresa.
    // Vazio: usa o aplicativo da rede (Cliente) e, se a rede também não tiver, o global do Previsio.
    public string? BlingClientId { get; set; }
    public string? BlingClientSecretProtegido { get; set; }

    // Tokens OAuth do Bling (criptografados com Data Protection)
    public string? BlingAccessTokenProtegido { get; set; }
    public string? BlingRefreshTokenProtegido { get; set; }
    public DateTime? BlingTokenExpiraEm { get; set; }

    public DateTime? UltimaSincronizacaoEm { get; set; }
    public bool Ativo { get; set; } = true;

    public bool BlingConectado
    {
        get { return !string.IsNullOrEmpty(BlingRefreshTokenProtegido); }
    }

    public bool TemEmailProprio
    {
        get { return !string.IsNullOrWhiteSpace(ImapHost) && !string.IsNullOrWhiteSpace(ImapUsuario); }
    }
}
