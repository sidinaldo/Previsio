namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Situação de um job agendado (recorrente) no Hangfire.
/// </summary>
public class ExecucaoAgendada
{
    public string Id { get; set; } = "";
    public DateTime? UltimaExecucaoEm { get; set; }
    // Estado do Hangfire da última execução: Succeeded, Failed, Processing, Enqueued, Scheduled (nova tentativa)...
    public string? UltimaSituacao { get; set; }
    public DateTime? ProximaExecucaoEm { get; set; }
    // Mensagem da última falha (do job ou do próprio agendamento)
    public string? Erro { get; set; }
}
