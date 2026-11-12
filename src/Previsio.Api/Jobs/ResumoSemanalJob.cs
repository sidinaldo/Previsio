using Hangfire;
using Previsio.Aplicacao.Resumos;

namespace Previsio.Api.Jobs;

/// <summary>
/// Roda de hora em hora e envia o resumo semanal das redes cujo dia e hora chegaram.
/// </summary>
public class ResumoSemanalJob
{
    private readonly ResumoSemanalService _resumos;

    public ResumoSemanalJob(ResumoSemanalService resumos)
    {
        _resumos = resumos;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _resumos.EnviarDevidosAsync(cancelamento);
    }
}
