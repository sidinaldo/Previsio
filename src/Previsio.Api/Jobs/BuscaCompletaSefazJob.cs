using Hangfire;
using Previsio.Aplicacao.BuscaSefaz;

namespace Previsio.Api.Jobs;

/// <summary>
/// De 15 em 15 minutos: busca completa na SEFAZ das empresas cuja hora de buscar chegou
/// (cada empresa espera o intervalo de BuscaSefaz:IntervaloMinutos entre uma busca e outra).
/// </summary>
public class BuscaCompletaSefazJob
{
    private readonly BuscaCompletaSefazService _busca;

    public BuscaCompletaSefazJob(BuscaCompletaSefazService busca)
    {
        _busca = busca;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 1200)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecutarAsync(CancellationToken cancelamento)
    {
        return _busca.ExecutarAsync(cancelamento);
    }
}
