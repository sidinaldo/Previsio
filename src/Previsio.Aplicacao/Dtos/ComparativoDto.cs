namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Peso de cada empresa e os fornecedores e categorias que mais consomem caixa no período
/// (contas a pagar pagas e em aberto com vencimento no período).
/// </summary>
public class ComparativoDto
{
    public decimal TotalSaidas { get; set; }
    public decimal TotalEntradas { get; set; }
    public int QuantidadeContas { get; set; }
    public List<EmpresaComparativoDto> Empresas { get; set; } = new List<EmpresaComparativoDto>();
    public List<ItemRankingDto> Fornecedores { get; set; } = new List<ItemRankingDto>();
    public List<ItemRankingDto> Categorias { get; set; } = new List<ItemRankingDto>();
}
