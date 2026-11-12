using Hangfire;
using Previsio.Aplicacao.Vencimentos;

namespace Previsio.Api.Jobs;

/// <summary>
/// De hora em hora: e-mail das contas que vencem hoje e amanhã, na hora escolhida por cada rede.
/// </summary>
public class AlertaVencimentosJob
{
    private readonly AlertaVencimentosService _alertas;

    public AlertaVencimentosJob(AlertaVencimentosService alertas)
    {
        _alertas = alertas;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _alertas.EnviarDevidosAsync(cancelamento);
    }
}
