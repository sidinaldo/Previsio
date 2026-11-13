namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Conta a receber do Bling na tela "Contas a receber".
/// </summary>
public class ContaReceberDto
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
    public string? Cliente { get; set; }
}
