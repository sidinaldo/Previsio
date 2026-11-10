namespace Previsio.Api.Dtos;

public class ClienteDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public string ImapUsuario { get; set; } = "";
    public bool Ativo { get; set; }
}
