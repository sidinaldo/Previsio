namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Sincronização com o Bling de uma rede, para o botão "Sincronizar com o Bling" das telas de contas.
/// </summary>
public class SituacaoSincronizacaoDto
{
    // Alguma sincronização na fila ou rodando (a tela espera ela acabar para recarregar)
    public bool EmExecucao { get; set; }
    // Sincronização mais recente entre as empresas conectadas da rede
    public DateTime? UltimaSincronizacaoEm { get; set; }
    // Antes disso o botão não sincroniza de novo (intervalo mínimo para não sobrecarregar o Bling)
    public DateTime? PodeSincronizarEm { get; set; }
}
