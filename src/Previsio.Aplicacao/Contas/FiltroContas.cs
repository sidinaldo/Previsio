namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Filtros da tela Contas a pagar (os mesmos da exportação). Datas são de vencimento.
/// </summary>
public class FiltroContas
{
    public int? EmpresaId { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }

    // Abertas, Vencidas, Pagas, Canceladas ou Todas (vazio = Todas)
    public string? Situacao { get; set; }

    // Previsio (veio de nota lida pelo Previsio), Bling (lançada direto no Bling) ou vazio
    public string? Origem { get; set; }

    // Fornecedor, número da NF ou empresa
    public string? Busca { get; set; }
    // Nome da categoria do Bling, ou FiltroContas.SemCategoria (só contas a pagar)
    public string? Categoria { get; set; }

    // Contas já conferidas no Bling que estão sem categoria
    public const string SemCategoria = "Sem categoria";
    // Contas com categoria no Bling que não está na lista da empresa no Previsio
    public const string CategoriaNaoIdentificada = "Categoria não identificada";

    // Grupo da forma de pagamento (GruposFormaPagamento: boleto, pix, debito-automatico, sem...),
    // ou FiltroContas.PrecisaPagar / FiltroContas.SaiSozinho
    public string? Forma { get; set; }

    // Contas que alguém precisa pagar (tudo menos os grupos que saem sozinhos da conta)
    public const string PrecisaPagar = "precisa-pagar";
    // Contas que saem sozinhas da conta (débito automático; FormasPagamento:GruposSaiSozinho)
    public const string SaiSozinho = "sai-sozinho";

    // vencimento, valor, fornecedor ou empresa ("-" na frente = decrescente)
    public string? Ordem { get; set; }
}
