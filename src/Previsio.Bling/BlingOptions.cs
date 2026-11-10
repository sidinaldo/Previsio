namespace Previsio.Bling;

/// <summary>
/// Configuração do aplicativo cadastrado na Área do integrador do Bling.
/// </summary>
public class BlingOptions
{
    public const string Secao = "Bling";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Precisa ser igual ao "Link de redirecionamento" cadastrado no app do Bling
    public string RedirectUri { get; set; } = "";

    public string ApiBaseUrl { get; set; } = "https://api.bling.com.br/Api/v3/";
    public string AuthorizeUrl { get; set; } = "https://www.bling.com.br/Api/v3/oauth/authorize";
    public string TokenUrl { get; set; } = "https://api.bling.com.br/Api/v3/oauth/token";

    // Limite oficial: 3 requisições por segundo por conta Bling
    public int IntervaloMinimoEntreRequisicoesMs { get; set; } = 350;
}
