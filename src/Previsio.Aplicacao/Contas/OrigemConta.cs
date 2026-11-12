namespace Previsio.Aplicacao.Contas;

/// <summary>
/// De onde veio a conta a pagar.
/// </summary>
public static class OrigemConta
{
    // Lançada pelo Previsio a partir de uma nota lida do e-mail
    public const string Previsio = "Previsio";

    // Lançada à mão direto no Bling
    public const string Bling = "Bling";
}
