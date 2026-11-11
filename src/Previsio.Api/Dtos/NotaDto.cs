namespace Previsio.Api.Dtos;

public class NotaDto
{
    public int Id { get; set; }
    public int? EmpresaId { get; set; }
    public string? EmpresaNome { get; set; }
    public string Numero { get; set; } = "";
    public string ChaveAcesso { get; set; } = "";
    public string Fornecedor { get; set; } = "";
    public string CnpjFornecedor { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public decimal ValorTotal { get; set; }
    public DateTime? PrimeiroVencimento { get; set; }
    public string Status { get; set; } = "";
    public string? MensagemErro { get; set; }
    public DateTime RecebidaEm { get; set; }

    // Preenchidos quando alguém marcou a nota como lançada manualmente no Bling
    public DateTime? ResolvidaManualmenteEm { get; set; }
    public string? ResolvidaManualmentePor { get; set; }
    public string? ObservacaoResolucao { get; set; }
}
