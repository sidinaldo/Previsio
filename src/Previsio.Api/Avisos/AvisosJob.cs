using Hangfire;

namespace Previsio.Api.Avisos;

/// <summary>
/// Roda depois da leitura de e-mails e do lançamento: atualiza os avisos e manda o e-mail dos novos.
/// </summary>
public class AvisosJob
{
    private readonly AvisosService _avisos;

    public AvisosJob(AvisosService avisos)
    {
        _avisos = avisos;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 120)]
    public async Task ExecutarAsync(CancellationToken cancelamento)
    {
        await _avisos.AtualizarAsync(null, cancelamento);
        await _avisos.EnviarNovosAsync(cancelamento);
    }
}
