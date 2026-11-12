using Hangfire;
using Previsio.Aplicacao.LeituraEmail;

namespace Previsio.Api.Jobs;

/// <summary>
/// Job do Hangfire que lê as caixas de e-mail (a cada 10 minutos). A regra fica no LeituraEmailService.
/// </summary>
public class LeituraEmailJob
{
    private readonly LeituraEmailService _leitura;

    public LeituraEmailJob(LeituraEmailService leitura)
    {
        _leitura = leitura;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _leitura.LerTodasAsync(cancelamento);
    }
}
