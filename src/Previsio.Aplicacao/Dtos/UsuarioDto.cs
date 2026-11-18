namespace Previsio.Aplicacao.Dtos;

public class UsuarioDto
{
    public int Id { get; set; }
    public int? ClienteId { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Perfil { get; set; } = "";
    public bool Ativo { get; set; }
    public DateTime? UltimoAcessoEm { get; set; }
    // Só no login e em /auth/eu: nome do ERP da rede do usuário nos textos do painel; nulo para o admin
    public string? ErpNome { get; set; }
}
