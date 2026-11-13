namespace Previsio.Core.Entidades;

/// <summary>
/// Espelho local das contas a receber do Bling, para o fluxo de caixa do painel
/// (entradas × saídas) e a tela Contas a receber. Situação usa SituacaoContaBling.
/// </summary>
public class ContaReceberSincronizada
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public long BlingId { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public int Situacao { get; set; }
    public long? BlingContatoId { get; set; }

    // Nome do cliente que vai pagar: o que vem na conta ou o contato do Bling guardado
    public string? ClienteNome { get; set; }

    public DateTime SincronizadaEm { get; set; } = DateTime.UtcNow;
}
