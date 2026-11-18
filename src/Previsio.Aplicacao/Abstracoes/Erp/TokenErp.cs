namespace Previsio.Aplicacao.Abstracoes.Erp;

public class TokenErp
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public int ExpiraEmSegundos { get; set; }
}
