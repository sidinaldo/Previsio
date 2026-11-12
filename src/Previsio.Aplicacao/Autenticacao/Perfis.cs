using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Autenticacao;

/// <summary>
/// Nomes dos perfis como aparecem no token e no [Authorize(Roles = ...)].
/// </summary>
public static class Perfis
{
    public const string Admin = "Admin";
    public const string Cliente = "Cliente";
    public const string Gestor = "Gestor";

    // Quem pode cadastrar e alterar usuários
    public const string GestaoUsuarios = Admin + "," + Gestor;

    public static string Nome(PerfilUsuario perfil)
    {
        if (perfil == PerfilUsuario.Admin)
        {
            return Admin;
        }
        if (perfil == PerfilUsuario.Gestor)
        {
            return Gestor;
        }
        return Cliente;
    }

    public static bool TentarLer(string? texto, out PerfilUsuario perfil)
    {
        string valor = (texto ?? "").Trim();

        if (string.Equals(valor, Admin, StringComparison.OrdinalIgnoreCase))
        {
            perfil = PerfilUsuario.Admin;
            return true;
        }
        if (string.Equals(valor, Gestor, StringComparison.OrdinalIgnoreCase))
        {
            perfil = PerfilUsuario.Gestor;
            return true;
        }
        if (string.Equals(valor, Cliente, StringComparison.OrdinalIgnoreCase))
        {
            perfil = PerfilUsuario.Cliente;
            return true;
        }

        perfil = PerfilUsuario.Cliente;
        return false;
    }
}
