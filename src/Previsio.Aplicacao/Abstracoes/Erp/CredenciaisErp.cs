namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Aplicativo (client id e secret) usado para autorizar o Previsio no ERP.
/// </summary>
public class CredenciaisErp
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public bool Completas
    {
        get { return !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret); }
    }
}
