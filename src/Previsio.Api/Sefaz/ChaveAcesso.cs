using System.Text.RegularExpressions;

namespace Previsio.Api.Sefaz;

/// <summary>
/// Chave de acesso da NF-e: 44 dígitos, com dígito verificador (módulo 11) no fim.
/// Posições: UF (2), ano e mês (4), CNPJ do emitente (14), modelo (2), série (3), número (9),
/// tipo de emissão (1), código numérico (8), dígito (1).
/// </summary>
public static class ChaveAcesso
{
    // 44 dígitos seguidos ou em grupos de 4 separados por espaço, ponto ou hífen (como sai no DANFE)
    private static readonly Regex Candidata = new Regex(@"(?<!\d)(\d{4}[\s.\-]?){10}\d{4}(?!\d)", RegexOptions.Compiled);

    public static bool Valida(string? chave)
    {
        if (chave == null || chave.Length != 44)
        {
            return false;
        }
        foreach (char caractere in chave)
        {
            if (caractere < '0' || caractere > '9')
            {
                return false;
            }
        }
        // Só NF-e (modelo 55)
        if (chave.Substring(20, 2) != "55")
        {
            return false;
        }
        return DigitoVerificador(chave.Substring(0, 43)) == chave[43] - '0';
    }

    /// <summary>
    /// Primeira chave válida no texto (nome de arquivo, assunto, corpo do e-mail, texto do PDF); nula se não houver.
    /// </summary>
    public static string? Procurar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }
        foreach (Match encontrada in Candidata.Matches(texto))
        {
            string chave = SomenteDigitos(encontrada.Value);
            if (Valida(chave))
            {
                return chave;
            }
        }
        return null;
    }

    public static string SomenteDigitos(string texto)
    {
        var digitos = new System.Text.StringBuilder(texto.Length);
        foreach (char caractere in texto)
        {
            if (caractere >= '0' && caractere <= '9')
            {
                digitos.Append(caractere);
            }
        }
        return digitos.ToString();
    }

    /// <summary>
    /// Módulo 11 com pesos de 2 a 9, da direita para a esquerda; resto 0 ou 1 dá dígito 0.
    /// </summary>
    public static int DigitoVerificador(string chaveSemDigito)
    {
        int soma = 0;
        int peso = 2;
        for (int i = chaveSemDigito.Length - 1; i >= 0; i--)
        {
            soma = soma + (chaveSemDigito[i] - '0') * peso;
            peso++;
            if (peso > 9)
            {
                peso = 2;
            }
        }
        int resto = soma % 11;
        if (resto < 2)
        {
            return 0;
        }
        return 11 - resto;
    }

    /// <summary>
    /// CNPJ do emitente (fornecedor) que vem dentro da chave.
    /// </summary>
    public static string CnpjEmitente(string chave)
    {
        return chave.Substring(6, 14);
    }

    /// <summary>
    /// Número da nota que vem dentro da chave, sem os zeros à esquerda.
    /// </summary>
    public static string Numero(string chave)
    {
        string numero = chave.Substring(25, 9).TrimStart('0');
        if (numero == "")
        {
            return "0";
        }
        return numero;
    }
}
