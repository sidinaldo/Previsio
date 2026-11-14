namespace Previsio.Aplicacao.Dtos;

public class EmpresaComparativoDto
{
    public int EmpresaId { get; set; }
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public decimal Saidas { get; set; }
    public decimal SaidasPagas { get; set; }
    public decimal SaidasEmAberto { get; set; }
    public decimal Entradas { get; set; }
    public int QuantidadeContas { get; set; }
    // Parte das saídas do grupo (0 a 100, uma casa)
    public decimal Percentual { get; set; }
}
