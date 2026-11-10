namespace Previsio.Api.Dtos;

public class UsuarioDto
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Perfil { get; set; } = "";
    public bool Ativo { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
}
