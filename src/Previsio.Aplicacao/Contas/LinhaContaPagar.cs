using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Conta sincronizada com a empresa e a nota de origem, já resolvidas na consulta.
/// </summary>
public class LinhaContaPagar
{
    public ContaPagarSincronizada Conta { get; set; } = null!;
    public string EmpresaNome { get; set; } = "";
    public string EmpresaCnpj { get; set; } = "";
    public ParcelaDaConta? Parcela { get; set; }

    public string? Fornecedor
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Conta.FornecedorNome))
            {
                return Conta.FornecedorNome;
            }
            if (Parcela != null)
            {
                return Parcela.Fornecedor;
            }
            return null;
        }
    }

    public string Origem
    {
        get
        {
            if (Parcela != null)
            {
                return OrigemConta.Previsio;
            }
            return OrigemConta.Bling;
        }
    }
}
