using Hangfire;
using Previsio.Aplicacao.Sincronizacao;

namespace Previsio.Api.Jobs;

/// <summary>
/// Job do Hangfire que traz as contas a pagar do Bling. A regra fica no SincronizacaoContasService.
/// </summary>
public class SincronizacaoContasJob
{
    private readonly SincronizacaoContasService _sincronizacao;

    public SincronizacaoContasJob(SincronizacaoContasService sincronizacao)
    {
        _sincronizacao = sincronizacao;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 900)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _sincronizacao.ExecutarAsync(cancelamento);
    }
}
