namespace Previsio.Api.Autenticacao;

public class TokenGerado
{
    public string Token { get; set; } = "";
    public DateTime ExpiraEm { get; set; }
}
