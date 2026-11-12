namespace Previsio.Api.Dtos;

/// <summary>
/// Conta a pagar do Bling na tela "Contas a pagar" (lançada pelo Previsio ou à mão).
/// </summary>
public class ContaPagarDto
{
    public int Id { get; set; }
    public long BlingId { get; set; }
    public int EmpresaId { get; set; }
    public string EmpresaNome { get; set; } = "";
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public int Situacao { get; set; }
    public string SituacaoTexto { get; set; } = "";
    public bool Vencida { get; set; }
    public string? Fornecedor { get; set; }

    // "Previsio" ou "Bling"
    public string Origem { get; set; } = "";
    public int? NotaFiscalId { get; set; }
    public string? NumeroNota { get; set; }
    public string? NumeroParcela { get; set; }
}
