using Hangfire;
using Previsio.Aplicacao.Notas;

namespace Previsio.Api.Jobs;

/// <summary>
/// Job do Hangfire que lança as notas no Bling (a cada 10 minutos e ao receber uma nota).
/// A regra fica no LancamentoNotaService.
/// </summary>
public class LancamentoNotaJob
{
    private readonly LancamentoNotaService _lancamento;

    public LancamentoNotaJob(LancamentoNotaService lancamento)
    {
        _lancamento = lancamento;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task LancarPendentesAsync(CancellationToken cancelamento)
    {
        return _lancamento.LancarPendentesAsync(cancelamento);
    }

    public Task<bool> LancarAsync(int notaId, CancellationToken cancelamento)
    {
        return _lancamento.LancarAsync(notaId, cancelamento);
    }
}
