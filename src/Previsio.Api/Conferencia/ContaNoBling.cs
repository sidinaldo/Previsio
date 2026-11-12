namespace Previsio.Api.Conferencia;

/// <summary>
/// Conta a pagar que já existe no Bling, candidata a ser a parcela lançada à mão.
/// </summary>
public class ContaNoBling
{
    public long Id { get; set; }
    public long? ContatoId { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public int Situacao { get; set; }
    // Só vêm do detalhe da conta; usados quando a rede exige o número da nota
    public string? NumeroDocumento { get; set; }
    public string? Historico { get; set; }
    public bool DetalheCarregado { get; set; }
}
