using System.Globalization;
using System.Security.Claims;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Lê do token quem está chamando a API e o que essa pessoa pode acessar.
/// </summary>
public static class UsuarioLogado
{
    public static bool EhAdmin(this ClaimsPrincipal usuario)
    {
        return usuario.IsInRole(Perfis.Admin);
    }

    public static int? UsuarioId(this ClaimsPrincipal usuario)
    {
        return LerInteiro(usuario, ClaimsPrevisio.UsuarioId);
    }

    /// <summary>
    /// Cliente ao qual o usuário pertence. Nulo para administradores.
    /// </summary>
    public static int? ClienteId(this ClaimsPrincipal usuario)
    {
        return LerInteiro(usuario, ClaimsPrevisio.ClienteId);
    }

    /// <summary>
    /// Administrador acessa qualquer cliente; os demais, só o próprio.
    /// </summary>
    public static bool PodeAcessarCliente(this ClaimsPrincipal usuario, int clienteId)
    {
        if (usuario.EhAdmin())
        {
            return true;
        }

        int? proprioCliente = usuario.ClienteId();
        return proprioCliente.HasValue && proprioCliente.Value == clienteId;
    }

    private static int? LerInteiro(ClaimsPrincipal usuario, string tipo)
    {
        string? texto = usuario.FindFirstValue(tipo);
        int valor;
        if (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out valor))
        {
            return valor;
        }
        return null;
    }
}
