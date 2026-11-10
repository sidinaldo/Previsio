using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class TokenBling
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiraEmSegundos { get; set; }

    [JsonPropertyName("token_type")]
    public string TipoToken { get; set; } = "";
}
