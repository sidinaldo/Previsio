using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Previsio.Data;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Verificações de acesso que dependem do banco (rotas identificadas por empresa).
/// </summary>
public static class ControleAcesso
{
    /// <summary>
    /// Para o usuário de cliente, confirma que a empresa é do cliente dele.
    /// Administrador sempre pode (a existência da empresa é tratada por quem chama).
    /// </summary>
    public static async Task<bool> PodeAcessarEmpresaAsync(
        PrevisioDbContext db, ClaimsPrincipal usuario, int empresaId, CancellationToken cancelamento)
    {
        if (usuario.EhAdmin())
        {
            return true;
        }

        int? clienteId = usuario.ClienteId();
        if (!clienteId.HasValue)
        {
            return false;
        }

        return await db.Empresas.AnyAsync(e => e.Id == empresaId && e.ClienteId == clienteId.Value, cancelamento);
    }
}
