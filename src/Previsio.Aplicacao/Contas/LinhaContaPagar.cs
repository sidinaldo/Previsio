using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;

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
    // Forma de pagamento na lista da empresa (preenchido na consulta)
    public string? FormaNome { get; set; }
    public string? FormaGrupoGuardado { get; set; }
    // Se o grupo da forma sai sozinho da conta (preenchido pelo serviço, com o appsettings)
    public bool SaiSozinho { get; set; }

    /// <summary>
    /// Grupo da forma (GruposFormaPagamento). Conta com forma que não está na lista da empresa
    /// conta como "outras"; sem forma, "sem".
    /// </summary>
    public string FormaGrupo
    {
        get
        {
            if (FormaGrupoGuardado != null)
            {
                return FormaGrupoGuardado;
            }
            if (Conta.FormaPagamentoBlingId.HasValue)
            {
                return GruposFormaPagamento.Outras;
            }
            return GruposFormaPagamento.Sem;
        }
    }

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
