using Hangfire;
using Previsio.Aplicacao.Contratos;

namespace Previsio.Api.Jobs;

/// <summary>
/// De hora em hora: avisos por e-mail do fim do teste grátis.
/// </summary>
public class AvisosTesteJob
{
    private readonly AvisosTesteService _avisos;

    public AvisosTesteJob(AvisosTesteService avisos)
    {
        _avisos = avisos;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _avisos.EnviarAsync(cancelamento);
    }
}
