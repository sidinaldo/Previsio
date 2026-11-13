namespace Previsio.Core.Painel.Modelos;

public class TotalEmpresa
{
    public int EmpresaId { get; set; }
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public int QuantidadeContas { get; set; }
    public decimal Valor { get; set; }
    // A receber no período (com o fluxo de caixa ligado)
    public decimal ValorReceber { get; set; }
    public int NotasPendentes { get; set; }
    public decimal ValorNotasPendentes { get; set; }
    public DateTime? ProximoVencimento { get; set; }
    public decimal ValorProximoVencimento { get; set; }
    public DateTime? UltimaSincronizacaoEm { get; set; }
}
