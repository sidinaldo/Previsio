namespace Previsio.Api.Dtos;

public class NovoClienteDto
{
    public string Nome { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string ImapSenha { get; set; } = "";
}
