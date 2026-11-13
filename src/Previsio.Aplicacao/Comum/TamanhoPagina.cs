namespace Previsio.Aplicacao.Comum;

/// <summary>
/// Quantos itens por página nas listas paginadas pela API (contas, notas recebidas, falhas).
/// A tela escolhe 10, 25, 50 ou 100; sem escolha (ou valor estranho) vale o padrão.
/// </summary>
public static class TamanhoPagina
{
    public const int Padrao = 25;
    public const int Maximo = 100;

    public static int Valido(int? tamanho)
    {
        if (!tamanho.HasValue || tamanho.Value < 1)
        {
            return Padrao;
        }
        if (tamanho.Value > Maximo)
        {
            return Maximo;
        }
        return tamanho.Value;
    }
}
