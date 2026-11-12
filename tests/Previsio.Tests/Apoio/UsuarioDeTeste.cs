using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Monta o usuário logado com as mesmas claims do token: como ControllerContext (o User
/// que o controller enxerga) ou como ClaimsPrincipal (para chamar o serviço direto).
/// </summary>
public static class UsuarioDeTeste
{
    public static ControllerContext Admin()
    {
        return Contexto(PrincipalAdmin());
    }

    /// <summary>
    /// Usuário de perfil Cliente, que só enxerga os dados do próprio cliente.
    /// </summary>
    public static ControllerContext DoCliente(int clienteId)
    {
        return Contexto(Principal(2, Perfis.Cliente, clienteId));
    }

    /// <summary>
    /// Gestor da rede: enxerga a própria rede e cuida dos usuários dela.
    /// </summary>
    public static ControllerContext Gestor(int clienteId, int usuarioId)
    {
        return Contexto(Principal(usuarioId, Perfis.Gestor, clienteId));
    }

    public static ClaimsPrincipal PrincipalAdmin()
    {
        return Principal(1, Perfis.Admin, null);
    }

    public static ClaimsPrincipal Principal(int usuarioId, string perfil, int? clienteId)
    {
        var claims = new List<Claim>();
        claims.Add(new Claim(ClaimsPrevisio.UsuarioId, usuarioId.ToString()));
        claims.Add(new Claim(ClaimsPrevisio.Perfil, perfil));
        if (clienteId.HasValue)
        {
            claims.Add(new Claim(ClaimsPrevisio.ClienteId, clienteId.Value.ToString()));
        }

        var identidade = new ClaimsIdentity(claims, "Teste", ClaimsPrevisio.Nome, ClaimsPrevisio.Perfil);
        return new ClaimsPrincipal(identidade);
    }

    private static ControllerContext Contexto(ClaimsPrincipal usuario)
    {
        var contextoHttp = new DefaultHttpContext();
        contextoHttp.User = usuario;

        var contexto = new ControllerContext();
        contexto.HttpContext = contextoHttp;
        return contexto;
    }
}
