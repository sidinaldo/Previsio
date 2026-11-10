using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Previsio.Data;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Roda a cada requisição, depois que a assinatura e a validade do token foram conferidas.
/// Recusa o token de usuário desativado, de cliente desativado ou emitido antes
/// de uma troca de senha. Sem isso o token continuaria valendo até expirar.
/// </summary>
public static class ValidacaoTokenUsuario
{
    public static async Task ValidarAsync(TokenValidatedContext contexto)
    {
        if (contexto.Principal == null)
        {
            contexto.Fail("Token sem usuário.");
            return;
        }

        int? usuarioId = contexto.Principal.UsuarioId();
        string carimbo = contexto.Principal.FindFirstValue(ClaimsPrevisio.Carimbo) ?? "";
        if (!usuarioId.HasValue || carimbo == "")
        {
            contexto.Fail("Token sem usuário.");
            return;
        }

        PrevisioDbContext db = contexto.HttpContext.RequestServices.GetRequiredService<PrevisioDbContext>();

        var situacao = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId.Value)
            .Select(u => new
            {
                u.Ativo,
                u.CarimboSeguranca,
                ClienteAtivo = u.Cliente == null || u.Cliente.Ativo
            })
            .FirstOrDefaultAsync(contexto.HttpContext.RequestAborted);

        if (situacao == null || !situacao.Ativo || !situacao.ClienteAtivo || situacao.CarimboSeguranca != carimbo)
        {
            contexto.Fail("Usuário inativo ou token revogado.");
        }
    }
}
