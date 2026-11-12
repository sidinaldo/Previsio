namespace Previsio.Aplicacao.Dtos;

public class NovoUsuarioDto
{
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Senha { get; set; } = "";

    // "Cliente", "Gestor" ou "Admin". O gestor só cadastra Cliente ou Gestor da própria rede
    public string Perfil { get; set; } = "Cliente";

    // Obrigatório para Cliente e Gestor; vazio para Admin. Para o gestor, é sempre a rede dele
    public int? ClienteId { get; set; }
}
