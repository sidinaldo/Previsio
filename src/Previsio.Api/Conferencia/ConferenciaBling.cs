using System.Text.RegularExpressions;
using Previsio.Core.Entidades;

namespace Previsio.Api.Conferencia;

/// <summary>
/// Compara as parcelas da nota com as contas do mesmo fornecedor no Bling. Cada conta serve
/// para uma parcela só; a escolhida é a de valor mais próximo e, empatando, a de vencimento mais próximo.
/// Nota sem parcelas (sem vencimentos no XML): procura uma conta com o valor total, vencendo em até 120 dias da emissão.
/// </summary>
public static class ConferenciaBling
{
    public const int DiasNotaSemParcelas = 120;

    public static async Task<ResultadoConferencia> ConferirAsync(
        NotaFiscal nota, List<ContaNoBling> candidatas, RegrasConferencia regras, Func<ContaNoBling, Task> completarDetalhe)
    {
        var resultado = new ResultadoConferencia();
        var usadas = new HashSet<long>();
        List<ContaNoBling> validas = candidatas
            .Where(c => c.Situacao != SituacaoContaBling.Cancelado && c.Situacao != SituacaoContaBling.Devolvido)
            .ToList();

        if (nota.Parcelas.Count == 0)
        {
            resultado.Esperadas = 1;
            DateTime inicio = nota.DataEmissao.Date;
            DateTime fim = inicio.AddDays(DiasNotaSemParcelas);
            List<ContaNoBling> opcoes = validas
                .Where(c => Math.Abs(c.Valor - nota.ValorTotal) <= regras.ToleranciaValor && c.Vencimento.Date >= inicio && c.Vencimento.Date <= fim)
                .OrderBy(c => Math.Abs(c.Valor - nota.ValorTotal))
                .ThenBy(c => c.Vencimento)
                .ToList();
            ContaNoBling? escolhida = await PrimeiraComNumeroAsync(opcoes, nota, regras, completarDetalhe);
            if (escolhida != null)
            {
                resultado.Encontradas.Add(new ParcelaEncontrada { Parcela = null, ContaId = escolhida.Id });
            }
            return resultado;
        }

        resultado.Esperadas = nota.Parcelas.Count;
        foreach (ParcelaNota parcela in nota.Parcelas.OrderBy(p => p.Vencimento).ThenBy(p => p.NumeroParcela))
        {
            List<ContaNoBling> opcoes = validas
                .Where(c => !usadas.Contains(c.Id)
                    && Math.Abs(c.Valor - parcela.Valor) <= regras.ToleranciaValor
                    && Math.Abs((c.Vencimento.Date - parcela.Vencimento.Date).Days) <= regras.ToleranciaDias)
                .OrderBy(c => Math.Abs(c.Valor - parcela.Valor))
                .ThenBy(c => Math.Abs((c.Vencimento.Date - parcela.Vencimento.Date).Days))
                .ToList();
            ContaNoBling? escolhida = await PrimeiraComNumeroAsync(opcoes, nota, regras, completarDetalhe);
            if (escolhida != null)
            {
                usadas.Add(escolhida.Id);
                resultado.Encontradas.Add(new ParcelaEncontrada { Parcela = parcela, ContaId = escolhida.Id });
            }
        }
        return resultado;
    }

    /// <summary>
    /// O número da nota aparece como número inteiro no documento ou no histórico (zeros à esquerda não importam).
    /// </summary>
    public static bool CitaNumero(string? texto, string numeroNota)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return false;
        }
        string numero = numeroNota.TrimStart('0');
        if (numero == "")
        {
            return false;
        }
        return Regex.IsMatch(texto, @"(?<!\d)0*" + Regex.Escape(numero) + @"(?!\d)");
    }

    private static async Task<ContaNoBling?> PrimeiraComNumeroAsync(
        List<ContaNoBling> opcoes, NotaFiscal nota, RegrasConferencia regras, Func<ContaNoBling, Task> completarDetalhe)
    {
        foreach (ContaNoBling opcao in opcoes)
        {
            if (!regras.ExigeNumeroNota)
            {
                return opcao;
            }
            if (!opcao.DetalheCarregado)
            {
                await completarDetalhe(opcao);
                opcao.DetalheCarregado = true;
            }
            if (CitaNumero(opcao.NumeroDocumento, nota.Numero) || CitaNumero(opcao.Historico, nota.Numero))
            {
                return opcao;
            }
        }
        return null;
    }
}
