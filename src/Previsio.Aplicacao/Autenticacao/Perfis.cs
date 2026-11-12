using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Autenticacao;

/// <summary>
/// Nomes dos perfis como aparecem no token e no [Authorize(Roles = ...)].
/// </summary>
public static class Perfis
{
    public const string Admin = "Admin";
    public const string Cliente = "Cliente";

    public static string Nome(PerfilUsuario perfil)
    {
        if (perfil == PerfilUsuario.Admin)
        {
            return Admin;
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
        if (string.Equals(valor, Cliente, StringComparison.OrdinalIgnoreCase))
        {
            perfil = PerfilUsuario.Cliente;
            return true;
        }

        perfil = PerfilUsuario.Cliente;
        return false;
    }
}
