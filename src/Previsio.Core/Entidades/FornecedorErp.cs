namespace Previsio.Core.Entidades;

/// <summary>
/// Nome de um contato (fornecedor) do ERP, guardado para não buscar de novo a cada sincronização.
/// Usado nas contas a pagar lançadas direto no ERP, que não têm nota no Previsio.
/// O id do contato vale só dentro da conta do ERP da empresa.
/// </summary>
public class FornecedorErp
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public long ErpContatoId { get; set; }
    public string Nome { get; set; } = "";
    public string? Documento { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
