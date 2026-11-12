using Hangfire;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;

namespace Previsio.Api.Servicos;

/// <summary>
/// Lê do Hangfire os jobs recorrentes e o resultado da última execução de cada um.
/// </summary>
public class AgendaJobsHangfire : IAgendaJobs
{
    private const int TamanhoMaximoErro = 500;

    private readonly JobStorage _armazenamento;

    public AgendaJobsHangfire(JobStorage armazenamento)
    {
        _armazenamento = armazenamento;
    }

    public List<ExecucaoAgendada> Agendados()
    {
        var lista = new List<ExecucaoAgendada>();
        IMonitoringApi monitor = _armazenamento.GetMonitoringApi();

        using (IStorageConnection conexao = _armazenamento.GetConnection())
        {
            foreach (RecurringJobDto recorrente in conexao.GetRecurringJobs())
            {
                var execucao = new ExecucaoAgendada();
                execucao.Id = recorrente.Id;
                execucao.UltimaExecucaoEm = recorrente.LastExecution;
                execucao.UltimaSituacao = recorrente.LastJobState;
                execucao.ProximaExecucaoEm = recorrente.NextExecution;
                execucao.Erro = recorrente.Error;

                bool falhou = recorrente.LastJobState == "Failed" || recorrente.LastJobState == "Scheduled";
                if (string.IsNullOrEmpty(execucao.Erro) && falhou && !string.IsNullOrEmpty(recorrente.LastJobId))
                {
                    execucao.Erro = UltimoErro(monitor, recorrente.LastJobId);
                }
                lista.Add(execucao);
            }
        }
        return lista;
    }

    // O histórico vem do mais novo para o mais antigo: a primeira falha é a mais recente
    private static string? UltimoErro(IMonitoringApi monitor, string idJob)
    {
        JobDetailsDto? detalhes = monitor.JobDetails(idJob);
        if (detalhes == null || detalhes.History == null)
        {
            return null;
        }
        foreach (StateHistoryDto estado in detalhes.History)
        {
            if (estado.StateName == "Failed" && estado.Data != null && estado.Data.ContainsKey("ExceptionMessage"))
            {
                string mensagem = estado.Data["ExceptionMessage"];
                if (mensagem.Length > TamanhoMaximoErro)
                {
                    return mensagem.Substring(0, TamanhoMaximoErro);
                }
                return mensagem;
            }
        }
        return null;
    }
}
