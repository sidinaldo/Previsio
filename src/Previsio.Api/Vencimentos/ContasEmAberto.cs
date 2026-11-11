using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Vencimentos;

/// <summary>
/// Contas a pagar em aberto no Bling (as sincronizadas), para avisos, painel e e-mails.
/// "Em aberto" = situação Em aberto ou Parcialmente paga no Bling.
/// </summary>
public static class ContasEmAberto
{
    public static IQueryable<ContaPagarSincronizada> Das(PrevisioDbContext db, List<int> empresas)
    {
        return db.ContasPagar.AsNoTracking()
            .Where(c => empresas.Contains(c.EmpresaId)
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago));
    }

    /// <summary>
    /// Vencidas há pelo menos N dias (1 = venceu ontem ou antes).
    /// </summary>
    public static IQueryable<ContaPagarSincronizada> Vencidas(PrevisioDbContext db, List<int> empresas, DateTime hoje, int diasDeAtraso)
    {
        DateTime limite = hoje.Date.AddDays(-Math.Max(diasDeAtraso, 1));
        return Das(db, empresas).Where(c => c.Vencimento <= limite);
    }
}
