namespace Previsio.Aplicacao.Dtos;

public class RespostaLoginDto
{
    // Enviar em todas as chamadas no header: Authorization: Bearer {token}
    public string Token { get; set; } = "";
    public DateTime ExpiraEm { get; set; }
    public UsuarioDto Usuario { get; set; } = new UsuarioDto();
}
