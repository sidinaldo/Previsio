using System.Globalization;
using System.Text;

namespace Previsio.Aplicacao.Categorias;

/// <summary>
/// Comparação de nomes de categoria entre os Blings das empresas: sem diferença de maiúsculas,
/// acentos e espaços nas pontas ("Fretes" = " fretes" = "FRETES").
/// </summary>
public static class RegrasCategoria
{
    // Tipo 2 no Bling = receita; as outras servem para despesa
    public const int TipoReceita = 2;

    public static string Normalizar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            return "";
        }
        string decomposto = nome.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sem = new StringBuilder();
        foreach (char letra in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) != UnicodeCategory.NonSpacingMark)
            {
                sem.Append(letra);
            }
        }
        return sem.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string ApenasDigitos(string? texto)
    {
        if (texto == null)
        {
            return "";
        }
        return new string(texto.Where(char.IsDigit).ToArray());
    }
}
