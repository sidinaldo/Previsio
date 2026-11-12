using Hangfire;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Jobs;

/// <summary>
/// Enfileira os jobs no Hangfire. Fica na API porque precisa conhecer as classes dos jobs.
/// </summary>
public class FilaJobsHangfire : IFilaJobs
{
    private readonly IBackgroundJobClient _jobs;

    public FilaJobsHangfire(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void LancarNota(int notaId)
    {
        _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarAsync(notaId, CancellationToken.None));
    }

    public void LancarPendentes()
    {
        _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarPendentesAsync(CancellationToken.None));
    }

    public void SincronizarContas()
    {
        _jobs.Enqueue<SincronizacaoContasJob>(j => j.ExecutarAsync(CancellationToken.None));
    }
}
