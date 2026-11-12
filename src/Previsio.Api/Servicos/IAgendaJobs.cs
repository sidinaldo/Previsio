namespace Previsio.Api.Servicos;

/// <summary>
/// Os jobs agendados e como foi a última execução de cada um (tela Saúde).
/// </summary>
public interface IAgendaJobs
{
    List<ExecucaoAgendada> Agendados();
}
