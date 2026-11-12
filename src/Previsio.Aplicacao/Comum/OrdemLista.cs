namespace Previsio.Aplicacao.Comum;

/// <summary>
/// Ordenação pedida pela tela no parâmetro "ordem": o nome da coluna, com "-" na frente
/// para decrescente ("valor" = menor primeiro, "-valor" = maior primeiro).
/// Cada serviço decide quais colunas aceita.
/// </summary>
public class OrdemLista
{
    public string Coluna { get; private set; }
    public bool Decrescente { get; private set; }

    private OrdemLista(string coluna, bool decrescente)
    {
        Coluna = coluna;
        Decrescente = decrescente;
    }

    /// <summary>
    /// Vazio ou nulo usa o padrão do serviço.
    /// </summary>
    public static OrdemLista Ler(string? ordem, string colunaPadrao, bool decrescentePadrao)
    {
        string texto = (ordem ?? "").Trim();
        if (texto == "")
        {
            return new OrdemLista(colunaPadrao, decrescentePadrao);
        }
        if (texto.StartsWith("-"))
        {
            return new OrdemLista(texto.Substring(1).ToLowerInvariant(), true);
        }
        return new OrdemLista(texto.ToLowerInvariant(), false);
    }
}
