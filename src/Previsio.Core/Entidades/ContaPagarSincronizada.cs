namespace Previsio.Core.Entidades;

/// <summary>
/// Espelho local das contas a pagar do Bling (inclusive as lançadas à mão),
/// usado para montar o painel sem consultar o Bling a cada acesso.
/// </summary>
public class ContaPagarSincronizada
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public long BlingId { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public int Situacao { get; set; }
    public long? BlingContatoId { get; set; }

    // Nome do fornecedor: o emitente da nota que gerou a conta ou, nas lançadas à mão,
    // o contato do Bling guardado em FornecedorBling. Preenchido na sincronização.
    public string? FornecedorNome { get; set; }

    public DateTime SincronizadaEm { get; set; } = DateTime.UtcNow;
}
