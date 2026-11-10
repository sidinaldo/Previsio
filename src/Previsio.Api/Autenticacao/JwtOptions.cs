namespace Previsio.Api.Autenticacao;

/// <summary>
/// Configuração dos tokens de acesso (seção "Jwt" do appsettings).
/// </summary>
public class JwtOptions
{
    public const string Secao = "Jwt";

    // HS256 exige pelo menos 32 bytes. Nunca deixe a chave no appsettings.json versionado.
    public const int TamanhoMinimoChave = 32;

    public string Emissor { get; set; } = "Previsio";
    public string Audiencia { get; set; } = "Previsio";
    public string Chave { get; set; } = "";
    public int ValidadeMinutos { get; set; } = 480;
}
