using System.Collections.Concurrent;

namespace Previsio.Bling;

/// <summary>
/// Garante um intervalo mínimo entre requisições da mesma conta Bling,
/// respeitando o limite de 3 requisições por segundo.
/// O limite é por conta, então cada empresa tem sua própria fila.
/// </summary>
public class LimitadorRequisicoes
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _travas =
        new ConcurrentDictionary<string, SemaphoreSlim>();

    private readonly ConcurrentDictionary<string, DateTime> _ultimaRequisicao =
        new ConcurrentDictionary<string, DateTime>();

    public async Task AguardarVezAsync(string conta, int intervaloMs, CancellationToken cancelamento)
    {
        SemaphoreSlim trava = _travas.GetOrAdd(conta, _ => new SemaphoreSlim(1, 1));
        await trava.WaitAsync(cancelamento);
        try
        {
            DateTime ultima;
            if (_ultimaRequisicao.TryGetValue(conta, out ultima))
            {
                TimeSpan decorrido = DateTime.UtcNow - ultima;
                int faltaMs = intervaloMs - (int)decorrido.TotalMilliseconds;
                if (faltaMs > 0)
                {
                    await Task.Delay(faltaMs, cancelamento);
                }
            }
            _ultimaRequisicao[conta] = DateTime.UtcNow;
        }
        finally
        {
            trava.Release();
        }
    }
}
