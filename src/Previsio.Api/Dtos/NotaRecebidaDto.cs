namespace Previsio.Api.Dtos;

/// <summary>
/// Nota lida do e-mail, lançada ou não no Bling, com as parcelas (tela "Notas recebidas").
/// </summary>
public class NotaRecebidaDto
{
    public int Id { get; set; }
    public int? EmpresaId { get; set; }
    public string? EmpresaNome { get; set; }
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public string ChaveAcesso { get; set; } = "";
    public string Fornecedor { get; set; } = "";
    public string CnpjFornecedor { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public decimal ValorTotal { get; set; }
    public DateTime? PrimeiroVencimento { get; set; }
    public string Status { get; set; } = "";
    public string? MensagemErro { get; set; }
    public DateTime RecebidaEm { get; set; }
    public DateTime? LancadaEm { get; set; }
    public List<ParcelaNotaDto> Parcelas { get; set; } = new List<ParcelaNotaDto>();

    // Nome da empresa dona da caixa em que a nota chegou, só quando não é a empresa do CNPJ da nota
    // (o fornecedor mandou para o e-mail de outra loja). Nulo quando chegou na caixa da rede.
    public string? CaixaDeOutraEmpresa { get; set; }

    // Preenchidos quando alguém marcou a nota como lançada manualmente no Bling
    public DateTime? ResolvidaManualmenteEm { get; set; }
    public string? ResolvidaManualmentePor { get; set; }
    public string? ObservacaoResolucao { get; set; }
}
