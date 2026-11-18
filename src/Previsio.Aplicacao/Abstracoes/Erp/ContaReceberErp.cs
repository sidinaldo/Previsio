namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Conta a receber como está no ERP. Situacao segue <see cref="Previsio.Core.Entidades.SituacaoConta"/>.
/// </summary>
public class ContaReceberErp
{
    public long Id { get; set; }
    public int Situacao { get; set; }
    // Nulo = o ERP mandou uma data que não deu para ler
    public DateTime? Vencimento { get; set; }
    public decimal Valor { get; set; }
    public long? ContatoId { get; set; }
    public string? ContatoNome { get; set; }
}
