namespace Previsio.Core.Entidades;

/// <summary>
/// Espelho local das contas a pagar do ERP (inclusive as lançadas à mão),
/// usado para montar o painel sem consultar o ERP a cada acesso.
/// </summary>
public class ContaPagarSincronizada
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public long ErpId { get; set; }
    public DateTime Vencimento { get; set; }
    public decimal Valor { get; set; }
    public int Situacao { get; set; }
    public long? ErpContatoId { get; set; }

    // Nome do fornecedor: o emitente da nota que gerou a conta ou, nas lançadas à mão,
    // o contato do ERP guardado em FornecedorErp. Preenchido na sincronização.
    public string? FornecedorNome { get; set; }

    public DateTime SincronizadaEm { get; set; }

    // Categoria no ERP (só vem no detalhe da conta, buscado aos poucos); nulo = sem categoria
    public long? CategoriaErpId { get; set; }
    // Quando a categoria foi conferida no ERP; nulo = ainda não conferida (ou a conta mudou)
    public DateTime? CategoriaConferidaEm { get; set; }

    // Forma de pagamento no ERP (vem na lista de contas); nulo = sem forma
    public long? FormaPagamentoErpId { get; set; }
}
