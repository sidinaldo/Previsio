namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Fornecedor a cadastrar no ERP, ativo. Documento com 11 dígitos é CPF (pessoa física);
/// os demais, CNPJ (pessoa jurídica).
/// </summary>
public class NovoContatoErp
{
    public string Nome { get; set; } = "";
    public string Documento { get; set; } = "";
}
