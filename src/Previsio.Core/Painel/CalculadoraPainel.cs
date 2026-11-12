using Previsio.Core.Entidades;
using Previsio.Core.Painel.Modelos;

namespace Previsio.Core.Painel;

/// <summary>
/// Monta os números do painel a partir das contas sincronizadas do Bling
/// e das notas que chegaram por e-mail mas ainda não viraram conta a pagar.
/// </summary>
public class CalculadoraPainel
{
    // Quantas notas pendentes o painel lista; as outras ficam em Notas pendentes
    public const int MaximoNotasParaResolver = 5;

    public ResumoPainel Calcular(
        DateTime hoje,
        int dias,
        IList<Empresa> empresas,
        IList<ContaPagarSincronizada> contas,
        IList<NotaFiscal> notasPendentes)
    {
        DateTime inicio = hoje.Date;
        DateTime fim = inicio.AddDays(dias - 1);

        var contasDoPeriodo = new List<ContaPagarSincronizada>();
        foreach (ContaPagarSincronizada conta in contas)
        {
            bool emAberto = conta.Situacao == SituacaoContaBling.EmAberto
                || conta.Situacao == SituacaoContaBling.ParcialmentePago;

            if (emAberto && conta.Vencimento.Date >= inicio && conta.Vencimento.Date <= fim)
            {
                contasDoPeriodo.Add(conta);
            }
        }

        var resumo = new ResumoPainel();
        resumo.Inicio = inicio;
        resumo.Fim = fim;
        resumo.TotalPeriodo = contasDoPeriodo.Sum(c => c.Valor);
        resumo.QuantidadeContas = contasDoPeriodo.Count;
        resumo.NotasPendentes = notasPendentes.Count;
        resumo.ValorNotasPendentes = notasPendentes.Sum(n => n.ValorTotal);

        DateTime fimSemanaAtual = inicio.AddDays(6);
        resumo.TotalSemanaAtual = contasDoPeriodo
            .Where(c => c.Vencimento.Date <= fimSemanaAtual)
            .Sum(c => c.Valor);

        DateTime inicioSemana = inicio;
        while (inicioSemana <= fim)
        {
            DateTime fimSemana = inicioSemana.AddDays(6);
            if (fimSemana > fim)
            {
                fimSemana = fim;
            }

            var semana = new TotalSemana();
            semana.Inicio = inicioSemana;
            semana.Fim = fimSemana;
            semana.Valor = contasDoPeriodo
                .Where(c => c.Vencimento.Date >= inicioSemana && c.Vencimento.Date <= fimSemana)
                .Sum(c => c.Valor);
            resumo.Semanas.Add(semana);

            inicioSemana = fimSemana.AddDays(1);
        }

        foreach (Empresa empresa in empresas)
        {
            var contasEmpresa = contasDoPeriodo.Where(c => c.EmpresaId == empresa.Id).ToList();
            var pendentesEmpresa = notasPendentes.Where(n => n.EmpresaId == empresa.Id).ToList();

            var total = new TotalEmpresa();
            total.EmpresaId = empresa.Id;
            total.Nome = empresa.Nome;
            total.Cnpj = empresa.Cnpj;
            total.QuantidadeContas = contasEmpresa.Count;
            total.Valor = contasEmpresa.Sum(c => c.Valor);
            total.NotasPendentes = pendentesEmpresa.Count;
            total.ValorNotasPendentes = pendentesEmpresa.Sum(n => n.ValorTotal);
            total.UltimaSincronizacaoEm = empresa.UltimaSincronizacaoEm;

            if (contasEmpresa.Count > 0)
            {
                DateTime proximo = contasEmpresa.Min(c => c.Vencimento.Date);
                total.ProximoVencimento = proximo;
                total.ValorProximoVencimento = contasEmpresa
                    .Where(c => c.Vencimento.Date == proximo)
                    .Sum(c => c.Valor);
            }

            resumo.Empresas.Add(total);
        }

        resumo.NotasParaResolver = MaisUrgentes(notasPendentes, empresas);
        return resumo;
    }

    /// <summary>
    /// As notas pendentes que vencem primeiro (pela primeira parcela). Nota sem parcelas vai
    /// para o fim; empate fica na ordem de chegada.
    /// </summary>
    public static List<NotaParaResolver> MaisUrgentes(IList<NotaFiscal> notasPendentes, IList<Empresa> empresas)
    {
        var lista = new List<NotaParaResolver>();
        foreach (NotaFiscal nota in notasPendentes)
        {
            var item = new NotaParaResolver();
            item.NotaId = nota.Id;
            item.Numero = nota.Numero;
            item.FornecedorNome = nota.NomeFornecedor;
            item.Valor = nota.ValorTotal;
            item.Status = nota.Status.ToString();
            item.MensagemErro = nota.MensagemErro;
            if (nota.Parcelas.Count > 0)
            {
                item.PrimeiroVencimento = nota.Parcelas.Min(p => p.Vencimento).Date;
            }

            Empresa? empresa = empresas.FirstOrDefault(e => e.Id == nota.EmpresaId);
            if (empresa != null)
            {
                item.EmpresaNome = empresa.Nome;
            }
            lista.Add(item);
        }

        return lista
            .OrderBy(n => n.PrimeiroVencimento.HasValue ? 0 : 1)
            .ThenBy(n => n.PrimeiroVencimento)
            .ThenBy(n => n.NotaId)
            .Take(MaximoNotasParaResolver)
            .ToList();
    }
}
