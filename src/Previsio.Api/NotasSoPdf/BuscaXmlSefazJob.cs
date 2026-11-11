using Hangfire;

namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// De 5 em 5 minutos: busca na SEFAZ o XML das notas que chegaram só em PDF.
/// </summary>
public class BuscaXmlSefazJob
{
    private readonly BuscaXmlSefazService _busca;

    public BuscaXmlSefazJob(BuscaXmlSefazService busca)
    {
        _busca = busca;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _busca.ProcessarPendentesAsync(cancelamento);
    }
}
