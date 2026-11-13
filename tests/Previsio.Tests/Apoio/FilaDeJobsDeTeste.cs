using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Api.Jobs;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Fila de jobs que só guarda o que foi enfileirado, sem executar nada. Serve tanto para os serviços
/// (IFilaJobs) quanto para os controllers que falam direto com o Hangfire (IBackgroundJobClient).
/// </summary>
public class FilaDeJobsDeTeste : IBackgroundJobClient, IFilaJobs
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

    public void LancarNota(int notaId)
    {
        Enfileirados.Add(Job.FromExpression<LancamentoNotaJob>(j => j.LancarAsync(notaId, CancellationToken.None)));
    }

    public void LancarPendentes()
    {
        Enfileirados.Add(Job.FromExpression<LancamentoNotaJob>(j => j.LancarPendentesAsync(CancellationToken.None)));
    }

    public void SincronizarContas()
    {
        Enfileirados.Add(Job.FromExpression<SincronizacaoContasJob>(j => j.ExecutarAsync(CancellationToken.None)));
    }

    public void SincronizarRede(int clienteId)
    {
        Enfileirados.Add(Job.FromExpression<SincronizacaoContasJob>(j => j.ExecutarAsync(clienteId, CancellationToken.None)));
    }

    // Os testes ligam para simular uma sincronização já rodando
    public bool EmAndamento { get; set; }

    public bool SincronizacaoEmAndamento()
    {
        return EmAndamento;
    }
}
