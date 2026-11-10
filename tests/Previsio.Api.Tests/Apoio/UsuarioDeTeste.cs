using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;

namespace Previsio.Api.Tests.Apoio;

/// <summary>
/// Monta o usuário logado que o controller enxerga em User, com as mesmas claims do token.
/// </summary>
public static class UsuarioDeTeste
{
    public static ControllerContext Admin()
    {
        var claims = new List<Claim>();
        claims.Add(new Claim(ClaimsPrevisio.UsuarioId, "1"));
        claims.Add(new Claim(ClaimsPrevisio.Perfil, Perfis.Admin));

        var identidade = new ClaimsIdentity(claims, "Teste", ClaimsPrevisio.Nome, ClaimsPrevisio.Perfil);
        var contextoHttp = new DefaultHttpContext();
        contextoHttp.User = new ClaimsPrincipal(identidade);

        var contexto = new ControllerContext();
        contexto.HttpContext = contextoHttp;
        return contexto;
    }

    /// <summary>
    /// Usuário de perfil Cliente, que só enxerga os dados do próprio cliente.
    /// </summary>
    public static ControllerContext DoCliente(int clienteId)
    {
        var claims = new List<Claim>();
        claims.Add(new Claim(ClaimsPrevisio.UsuarioId, "2"));
        claims.Add(new Claim(ClaimsPrevisio.Perfil, Perfis.Cliente));
        claims.Add(new Claim(ClaimsPrevisio.ClienteId, clienteId.ToString()));

        var identidade = new ClaimsIdentity(claims, "Teste", ClaimsPrevisio.Nome, ClaimsPrevisio.Perfil);
        var contextoHttp = new DefaultHttpContext();
        contextoHttp.User = new ClaimsPrincipal(identidade);

        var contexto = new ControllerContext();
        contexto.HttpContext = contextoHttp;
        return contexto;
    }
}
