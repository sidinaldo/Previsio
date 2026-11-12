using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Sincronizacao;

/// <summary>
/// Contas que venceram antes da janela da sincronização e ainda estão em aberto no Previsio.
/// A lista do Bling não traz mais essas contas, então cada uma é conferida pelo id até ser paga,
/// cancelada ou excluída. Sem isso, a conta paga depois de sair da janela ficaria vencida para sempre.
/// </summary>
public class ContasAntigasEmAberto
{
    private readonly PrevisioDbContext _db;
    private readonly BlingClient _bling;
    private readonly ILogger _logger;

    public ContasAntigasEmAberto(PrevisioDbContext db, BlingClient bling, ILogger logger)
    {
        _db = db;
        _bling = bling;
        _logger = logger;
    }

    /// <summary>
    /// Confere até "limite" contas (as conferidas há mais tempo primeiro). Quem chama salva.
    /// Devolve quantas foram conferidas no Bling.
    /// </summary>
    public async Task<int> ConferirAsync(
        Empresa empresa,
        string conta,
        string token,
        DateTime inicioJanela,
        int limite,
        DateTime agoraUtc,
        CancellationToken cancelamento)
    {
        List<ContaPagarSincronizada> antigas = await _db.ContasPagar
            .Where(c => c.EmpresaId == empresa.Id
                && c.Vencimento < inicioJanela
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago))
            .OrderBy(c => c.SincronizadaEm)
            .ThenBy(c => c.Id)
            .Take(limite)
            .ToListAsync(cancelamento);

        int conferidas = 0;
        foreach (ContaPagarSincronizada local in antigas)
        {
            ContaPagarBling? remota;
            try
            {
                remota = await _bling.ObterContaPagarAsync(conta, token, local.BlingId, cancelamento);
            }
            catch (BlingException erro)
            {
                if (erro.StatusCode == HttpStatusCode.NotFound)
                {
                    // Excluída no Bling: sai do Previsio, como acontece dentro da janela
                    conferidas++;
                    _db.ContasPagar.Remove(local);
                    continue;
                }
                // Outro erro (token, limite, rede): para aqui e tenta de novo na próxima sincronização
                _logger.LogWarning(erro, "Não foi possível conferir a conta {Conta} no Bling da empresa {EmpresaId}",
                    local.BlingId, empresa.Id);
                break;
            }

            conferidas++;
            if (remota == null)
            {
                continue;
            }

            local.Situacao = remota.Situacao;
            local.Valor = remota.Valor;
            DateTime vencimento;
            if (DateTime.TryParseExact(remota.Vencimento, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out vencimento))
            {
                local.Vencimento = vencimento.Date;
            }
            local.SincronizadaEm = agoraUtc;
        }
        return conferidas;
    }
}
