namespace Previsio.Api.Dtos;

public class NovoUsuarioDto
{
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";

    // "Cliente" ou "Admin"
    public string Perfil { get; set; } = "Cliente";

    // Obrigatório para o perfil Cliente; deve ficar vazio para Admin
    public int? ClienteId { get; set; }
}
