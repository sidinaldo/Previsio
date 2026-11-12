namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Pede para rodar um job em segundo plano assim que possível (implementado na API com o Hangfire).
/// </summary>
public interface IFilaJobs
{
    // Lança no Bling uma nota que voltou para a fila (reprocessar)
    void LancarNota(int notaId);

    // Lança as notas que estavam esperando (por exemplo, a conexão com o Bling)
    void LancarPendentes();

    // Traz do Bling as contas a pagar de todas as empresas conectadas
    void SincronizarContas();
}
