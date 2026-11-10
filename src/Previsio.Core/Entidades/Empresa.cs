namespace Previsio.Core.Entidades;

/// <summary>
/// Cada CNPJ do cliente. Cada empresa tem a sua própria conta no Bling.
/// </summary>
public class Empresa
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";

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
}
