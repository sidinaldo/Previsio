namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Filtros da tela Contas a receber (os mesmos da exportação). Datas são de vencimento.
/// </summary>
public class FiltroContasReceber
{
    public int? EmpresaId { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
    // Abertas, Vencidas, Recebidas, Canceladas ou Todas (vazio = Todas)
    public string? Situacao { get; set; }
    // Cliente ou empresa
    public string? Busca { get; set; }
    // vencimento, valor, cliente ou empresa ("-" na frente = decrescente)
    public string? Ordem { get; set; }
}
