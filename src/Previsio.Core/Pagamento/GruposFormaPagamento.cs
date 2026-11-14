using System.Globalization;
using System.Text;

namespace Previsio.Core.Pagamento;

/// <summary>
/// Grupos em que o Previsio separa as formas de pagamento do Bling: o que precisa ser pago
/// (boleto, Pix, transferência...) e o que sai sozinho da conta (débito automático).
/// O grupo vem do tipo da forma no Bling (código tPag da NF-e); débito automático vem do nome,
/// porque a NF-e não tem um código só para ele.
/// </summary>
public static class GruposFormaPagamento
{
    public const string Boleto = "boleto";
    public const string Pix = "pix";
    public const string Transferencia = "transferencia";
    public const string Cartao = "cartao";
    public const string Dinheiro = "dinheiro";
    public const string DebitoAutomatico = "debito-automatico";
    public const string Outras = "outras";
    // Conta sem forma de pagamento no Bling
    public const string Sem = "sem";

    // Códigos tPag que não dizem como pagar: não servem para escolher a forma pelo XML
    public const int TipoSemPagamento = 90;
    public const int TipoOutros = 99;

    /// <summary>Todos os grupos, na ordem das telas.</summary>
    public static readonly string[] Todos =
    {
        Boleto, Pix, Transferencia, Cartao, Dinheiro, DebitoAutomatico, Outras, Sem
    };

    public static string Nome(string grupo)
    {
        if (grupo == Boleto)
        {
            return "Boleto";
        }
        if (grupo == Pix)
        {
            return "Pix";
        }
        if (grupo == Transferencia)
        {
            return "Transferência/depósito";
        }
        if (grupo == Cartao)
        {
            return "Cartão";
        }
        if (grupo == Dinheiro)
        {
            return "Dinheiro";
        }
        if (grupo == DebitoAutomatico)
        {
            return "Débito automático";
        }
        if (grupo == Sem)
        {
            return "Sem forma";
        }
        return "Outras";
    }

    public static bool Valido(string? grupo)
    {
        return grupo != null && Todos.Contains(grupo);
    }

    /// <summary>
    /// Grupo de uma forma do Bling. "termosDebitoAutomatico" são trechos do nome (sem acento e
    /// maiúsculas) que fazem a forma contar como débito automático, seja qual for o tipo.
    /// </summary>
    public static string Classificar(int tipoPagamento, string? descricao, IEnumerable<string> termosDebitoAutomatico)
    {
        string nome = Normalizar(descricao);
        if (nome != "")
        {
            foreach (string termo in termosDebitoAutomatico)
            {
                string procurado = Normalizar(termo);
                if (procurado != "" && nome.Contains(procurado))
                {
                    return DebitoAutomatico;
                }
            }
        }

        if (tipoPagamento == 15)
        {
            return Boleto;
        }
        if (tipoPagamento == 17 || tipoPagamento == 20)
        {
            return Pix;
        }
        if (tipoPagamento == 16 || tipoPagamento == 18)
        {
            return Transferencia;
        }
        if (tipoPagamento == 3 || tipoPagamento == 4)
        {
            return Cartao;
        }
        if (tipoPagamento == 1)
        {
            return Dinheiro;
        }
        return Outras;
    }

    /// <summary>Sem diferença de maiúsculas, acentos e espaços nas pontas.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return "";
        }
        string decomposto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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
}
