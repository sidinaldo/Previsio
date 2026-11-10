namespace Previsio.Bling;

/// <summary>
/// Client ID e Client Secret de um aplicativo cadastrado no Bling.
/// Pode ser o aplicativo global do Previsio (appsettings) ou o aplicativo privado
/// criado dentro da conta Bling de uma empresa.
/// </summary>
public class CredenciaisBling
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public bool Completas
    {
        get { return !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret); }
    }
}
