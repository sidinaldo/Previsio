namespace Previsio.Aplicacao.Comum;

/// <summary>
/// Termo da caixa de busca das listas: o texto (para nomes e número da nota) e só os dígitos
/// (para CNPJ e chave de acesso, digitados com ou sem pontuação).
/// </summary>
public class BuscaTexto
{
    // Com menos dígitos que isso, "1" acharia quase todo CNPJ
    private const int MinimoDigitos = 3;

    public string Texto { get; private set; }
    public string Digitos { get; private set; }

    private BuscaTexto(string texto, string digitos)
    {
        Texto = texto;
        Digitos = digitos;
    }

    /// <summary>
    /// Nulo quando a busca está vazia (não filtra).
    /// </summary>
    public static BuscaTexto? Ler(string? busca)
    {
        string texto = (busca ?? "").Trim().ToLower();
        if (texto == "")
        {
            return null;
        }

        string digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length < MinimoDigitos)
        {
            digitos = "";
        }
        return new BuscaTexto(texto, digitos);
    }

    /// <summary>
    /// Se procura também em CNPJ e chave de acesso (só com dígitos suficientes).
    /// </summary>
    public bool TemDigitos
    {
        get { return Digitos != ""; }
    }
}
