namespace Previsio.Core.Entidades;

/// <summary>
/// O valor padrão (zero) é o perfil com menos acesso.
/// </summary>
public enum PerfilUsuario
{
    // Usuário comum da rede: vê os dados da rede, não mexe em usuários
    Cliente = 0,

    // Equipe da Soft.IO: vê todas as redes
    Admin = 1,

    // Responsável pela rede: além do que o Cliente vê, cadastra e altera os usuários da rede
    Gestor = 2
}
