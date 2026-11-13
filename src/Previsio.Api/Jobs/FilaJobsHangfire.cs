using Hangfire;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Jobs;

/// <summary>
/// Enfileira os jobs no Hangfire. Fica na API porque precisa conhecer as classes dos jobs.
/// </summary>
public class FilaJobsHangfire : IFilaJobs
{
    private readonly IBackgroundJobClient _jobs;
    private readonly IExecucaoJobs _execucao;

    public FilaJobsHangfire(IBackgroundJobClient jobs, IExecucaoJobs execucao)
    {
        _jobs = jobs;
        _execucao = execucao;
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

    public void SincronizarRede(int clienteId)
    {
        _jobs.Enqueue<SincronizacaoContasJob>(j => j.ExecutarAsync(clienteId, CancellationToken.None));
    }

    public bool SincronizacaoEmAndamento()
    {
        return _execucao.EstaEmExecucao(typeof(SincronizacaoContasJob), nameof(SincronizacaoContasJob.ExecutarAsync));
    }
}
