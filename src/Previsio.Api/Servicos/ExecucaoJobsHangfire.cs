using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Previsio.Api.Servicos;

/// <summary>
/// Consulta o Hangfire: o job está em execução enquanto aparece numa fila ou na lista dos que estão processando.
/// </summary>
public class ExecucaoJobsHangfire : IExecucaoJobs
{
    // Os jobs do Previsio são poucos; 100 por lista cobre com folga
    private const int Limite = 100;

    private readonly JobStorage _armazenamento;

    public ExecucaoJobsHangfire(JobStorage armazenamento)
    {
        _armazenamento = armazenamento;
    }

    public bool EstaEmExecucao(Type tipoJob, string metodo)
    {
        IMonitoringApi monitor = _armazenamento.GetMonitoringApi();

        foreach (KeyValuePair<string, ProcessingJobDto> item in monitor.ProcessingJobs(0, Limite))
        {
            if (EhOJob(item.Value.Job, tipoJob, metodo))
            {
                return true;
            }
        }

        foreach (QueueWithTopEnqueuedJobsDto fila in monitor.Queues())
        {
            foreach (KeyValuePair<string, EnqueuedJobDto> item in monitor.EnqueuedJobs(fila.Name, 0, Limite))
            {
                if (item.Value.InEnqueuedState && EhOJob(item.Value.Job, tipoJob, metodo))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool EhOJob(Job? job, Type tipoJob, string metodo)
    {
        if (job == null)
        {
            return false;
        }
        return job.Type == tipoJob && job.Method.Name == metodo;
    }
}
