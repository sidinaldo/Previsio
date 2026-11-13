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
    // Nome da categoria na lista da empresa (preenchido na consulta)
    public string? CategoriaNome { get; set; }
    public ParcelaDaConta? Parcela { get; set; }

    /// <summary>
    /// O que mostrar na coluna Categoria: o nome; "Categoria não identificada" quando a conta tem
    /// categoria no Bling que não está na lista da empresa; nulo quando não tem categoria.
    /// </summary>
    public string? Categoria
    {
        get
        {
            if (CategoriaNome != null)
            {
                return CategoriaNome;
            }
            if (Conta.CategoriaBlingId.HasValue)
            {
                return FiltroContas.CategoriaNaoIdentificada;
            }
            return null;
        }
    }

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
