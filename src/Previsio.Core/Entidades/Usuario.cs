namespace Previsio.Core.Entidades;

/// <summary>
/// Pessoa que acessa a API. O administrador (equipe do Previsio) não tem cliente
/// e enxerga todos; o usuário de cliente só enxerga os dados do próprio cliente.
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    // Nulo para administradores, obrigatório para usuários de cliente
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Cliente;

    // Vai dentro do token. Trocar o valor invalida todos os tokens já emitidos
    // (troca de senha, desativação).
    public string CarimboSeguranca { get; set; } = Guid.NewGuid().ToString("N");

    // "Esqueci minha senha": hash (SHA-256) do token enviado por e-mail e até quando ele vale.
    // O token em si nunca é gravado; um pedido novo substitui o anterior.
    public string? RedefinicaoSenhaHash { get; set; }
    public DateTime? RedefinicaoSenhaExpiraEm { get; set; }

    // Quando abriu o sininho pela última vez: avisos criados depois disso contam como novos
    public DateTime? AvisosVistosEm { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoAcessoEm { get; set; }
}
