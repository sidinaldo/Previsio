using Hangfire;
using Hangfire.Common;
using Hangfire.States;

namespace Previsio.Api.Tests.Apoio;

/// <summary>
/// Fila do Hangfire que só guarda os jobs enfileirados, sem executar nada.
/// </summary>
public class FilaDeJobsDeTeste : IBackgroundJobClient
{
    public List<Job> Enfileirados { get; } = new List<Job>();

    public string Create(Job job, IState state)
    {
        Enfileirados.Add(job);
        return Enfileirados.Count.ToString();
    }

    public bool ChangeState(string jobId, IState state, string expectedState)
    {
        return true;
    }
}
