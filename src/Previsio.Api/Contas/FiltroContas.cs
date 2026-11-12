namespace Previsio.Api.Contas;

/// <summary>
/// Filtros da tela Contas a pagar (os mesmos da exportação). Datas são de vencimento.
/// </summary>
public class FiltroContas
{
    public int? EmpresaId { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }

    // Abertas, Vencidas, Pagas, Canceladas ou Todas (vazio = Todas)
    public string? Situacao { get; set; }

    // Previsio (veio de nota lida pelo Previsio), Bling (lançada direto no Bling) ou vazio
    public string? Origem { get; set; }

    // Fornecedor, número da NF ou empresa
    public string? Busca { get; set; }

    // vencimento, valor, fornecedor ou empresa ("-" na frente = decrescente)
    public string? Ordem { get; set; }
}
