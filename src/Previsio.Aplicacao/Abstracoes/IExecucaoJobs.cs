namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Diz se um job em segundo plano ainda está na fila ou rodando (a tela espera ele acabar para atualizar).
/// </summary>
public interface IExecucaoJobs
{
    bool EstaEmExecucao(Type tipoJob, string metodo);
}
