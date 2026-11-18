namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Contato (fornecedor ou cliente) no ERP. Documento é o CNPJ ou CPF, só com números.
/// </summary>
public class ContatoErp
{
    public long Id { get; set; }
    public string Nome { get; set; } = "";
    public string Documento { get; set; } = "";
}
