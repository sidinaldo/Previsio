namespace Previsio.Core.Painel.Modelos;

/// <summary>
/// Nota que ainda não virou conta a pagar no Bling, na lista "Notas para resolver" do painel.
/// Fica de fora da previsão até ser lançada, por isso aparece com o primeiro vencimento.
/// </summary>
public class NotaParaResolver
{
    public int NotaId { get; set; }
    public string Numero { get; set; } = "";
    // Nulo quando o CNPJ da nota não é de nenhuma empresa ativa cadastrada
    public string? EmpresaNome { get; set; }
    public string FornecedorNome { get; set; } = "";
    public decimal Valor { get; set; }
    // Vencimento da primeira parcela; nulo quando o arquivo da nota não trouxe parcelas
    public DateTime? PrimeiroVencimento { get; set; }
    public string Status { get; set; } = "";
    public string? MensagemErro { get; set; }
}
