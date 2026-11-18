namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Conta a pagar como está no ERP. Situacao segue <see cref="Previsio.Core.Entidades.SituacaoConta"/>.
/// </summary>
public class ContaPagarErp
{
    public long Id { get; set; }
    public int Situacao { get; set; }
    // Nulo = o ERP mandou uma data que não deu para ler
    public DateTime? Vencimento { get; set; }
    public decimal Valor { get; set; }
    public long? ContatoId { get; set; }
    public string? NumeroDocumento { get; set; }
    public string? Historico { get; set; }
    // Só vem no detalhe (ObterContaPagarAsync); nulo = sem categoria
    public long? CategoriaId { get; set; }
    public long? FormaPagamentoId { get; set; }
}
