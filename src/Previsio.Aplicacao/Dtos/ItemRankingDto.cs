namespace Previsio.Aplicacao.Dtos;

public class ItemRankingDto
{
    public string Nome { get; set; } = "";
    public decimal Valor { get; set; }
    public int Quantidade { get; set; }
    // Parte das saídas do período (0 a 100, uma casa)
    public decimal Percentual { get; set; }
    // "categoria", "sem", "conferir"... nas categorias; "fornecedor" ou "outros" nos fornecedores
    public string Tipo { get; set; } = "";
}
