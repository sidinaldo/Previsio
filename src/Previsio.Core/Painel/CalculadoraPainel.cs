using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;
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

    // Quantas categorias o card mostra pelo nome; as outras viram "Outras"
    public const int MaximoCategorias = 6;

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
    /// Fluxo de caixa: soma as contas a receber em aberto do período às semanas e às empresas e
    /// calcula o saldo (entradas - saídas). As vencidas e não recebidas ficam fora do saldo,
    /// num total à parte. Chamar depois de Calcular, com as contas a receber da rede.
    /// </summary>
    public void AplicarEntradas(ResumoPainel resumo, IList<ContaReceberSincronizada> contasReceber)
    {
        resumo.FluxoCaixaAtivo = true;
        DateTime inicio = resumo.Inicio.Date;
        DateTime fim = resumo.Fim.Date;

        var doPeriodo = new List<ContaReceberSincronizada>();
        foreach (ContaReceberSincronizada conta in contasReceber)
        {
            bool emAberto = conta.Situacao == SituacaoContaBling.EmAberto
                || conta.Situacao == SituacaoContaBling.ParcialmentePago;
            if (!emAberto)
            {
                continue;
            }
            if (conta.Vencimento.Date < inicio)
            {
                resumo.TotalReceberVencido = resumo.TotalReceberVencido + conta.Valor;
                resumo.QuantidadeReceberVencido = resumo.QuantidadeReceberVencido + 1;
            }
            else if (conta.Vencimento.Date <= fim)
            {
                doPeriodo.Add(conta);
            }
        }

        resumo.TotalEntradasPeriodo = doPeriodo.Sum(c => c.Valor);
        resumo.QuantidadeContasReceber = doPeriodo.Count;
        resumo.SaldoPeriodo = resumo.TotalEntradasPeriodo - resumo.TotalPeriodo;

        decimal acumulado = 0m;
        foreach (TotalSemana semana in resumo.Semanas)
        {
            semana.Entradas = doPeriodo
                .Where(c => c.Vencimento.Date >= semana.Inicio && c.Vencimento.Date <= semana.Fim)
                .Sum(c => c.Valor);
            semana.Saldo = semana.Entradas - semana.Valor;
            acumulado = acumulado + semana.Saldo;
            semana.SaldoAcumulado = acumulado;
        }

        foreach (TotalEmpresa empresa in resumo.Empresas)
        {
            empresa.ValorReceber = doPeriodo.Where(c => c.EmpresaId == empresa.EmpresaId).Sum(c => c.Valor);
        }
    }

    /// <summary>
    /// Saídas em aberto do período por categoria. "nomes" leva a chave EmpresaId:BlingId da
    /// categoria ao nome dela no Bling da empresa; categorias de mesmo nome em empresas diferentes
    /// somam juntas. Mostra as 6 maiores e junta o resto em "Outras"; no fim vêm "Sem categoria"
    /// (conferidas sem categoria) e "A conferir" (a sincronização ainda não abriu a conta no Bling).
    /// </summary>
    public void AplicarCategorias(ResumoPainel resumo, IList<ContaPagarSincronizada> contas, IDictionary<string, string> nomes)
    {
        DateTime inicio = resumo.Inicio.Date;
        DateTime fim = resumo.Fim.Date;
        var porNome = new Dictionary<string, TotalCategoria>(StringComparer.OrdinalIgnoreCase);
        var sem = new TotalCategoria { Nome = "Sem categoria", Tipo = TotalCategoria.TipoSem };
        var aConferir = new TotalCategoria { Nome = "A conferir", Tipo = TotalCategoria.TipoAConferir };
        var naoIdentificada = new TotalCategoria { Nome = TotalCategoria.NomeNaoIdentificada, Tipo = TotalCategoria.TipoNaoIdentificada };

        foreach (ContaPagarSincronizada conta in contas)
        {
            bool emAberto = conta.Situacao == SituacaoContaBling.EmAberto || conta.Situacao == SituacaoContaBling.ParcialmentePago;
            if (!emAberto || conta.Vencimento.Date < inicio || conta.Vencimento.Date > fim)
            {
                continue;
            }

            TotalCategoria destino;
            string? nome = null;
            if (conta.CategoriaBlingId.HasValue)
            {
                nomes.TryGetValue(conta.EmpresaId + ":" + conta.CategoriaBlingId.Value, out nome);
            }
            if (nome != null)
            {
                string chave = nome.Trim();
                if (!porNome.TryGetValue(chave, out destino!))
                {
                    destino = new TotalCategoria { Nome = chave, Tipo = TotalCategoria.TipoCategoria };
                    porNome[chave] = destino;
                }
            }
            else if (conta.CategoriaBlingId.HasValue)
            {
                destino = naoIdentificada;
            }
            else if (!conta.CategoriaConferidaEm.HasValue)
            {
                destino = aConferir;
            }
            else
            {
                destino = sem;
            }
            destino.Valor = destino.Valor + conta.Valor;
            destino.Quantidade = destino.Quantidade + 1;
        }

        List<TotalCategoria> ordenadas = porNome.Values
            .OrderByDescending(c => c.Valor)
            .ThenBy(c => c.Nome)
            .ToList();
        var lista = ordenadas.Take(MaximoCategorias).ToList();
        List<TotalCategoria> resto = ordenadas.Skip(MaximoCategorias).ToList();
        if (resto.Count > 0)
        {
            lista.Add(new TotalCategoria
            {
                Nome = "Outras (" + resto.Count + ")",
                Tipo = TotalCategoria.TipoOutras,
                Valor = resto.Sum(c => c.Valor),
                Quantidade = resto.Sum(c => c.Quantidade)
            });
        }
        if (sem.Quantidade > 0)
        {
            lista.Add(sem);
        }
        if (naoIdentificada.Quantidade > 0)
        {
            lista.Add(naoIdentificada);
        }
        if (aConferir.Quantidade > 0)
        {
            lista.Add(aConferir);
        }
        resumo.Categorias = lista;
    }

    /// <summary>
    /// Saídas em aberto do período por grupo de forma de pagamento. "grupos" leva a chave EmpresaId:BlingId
    /// da forma ao grupo dela; conta com forma fora da lista conta como "outras" e sem forma, "sem forma".
    /// Os grupos de "gruposSaiSozinho" somam no que sai sozinho da conta; o resto, no que precisa pagar.
    /// Ordem: primeiro o que precisa pagar, do maior para o menor; "sem forma" no fim dele.
    /// </summary>
    public void AplicarFormas(
        ResumoPainel resumo, IList<ContaPagarSincronizada> contas, IDictionary<string, string> grupos,
        IList<string> gruposSaiSozinho)
    {
        DateTime inicio = resumo.Inicio.Date;
        DateTime fim = resumo.Fim.Date;
        var porGrupo = new Dictionary<string, TotalFormaPagamento>();

        foreach (ContaPagarSincronizada conta in contas)
        {
            bool emAberto = conta.Situacao == SituacaoContaBling.EmAberto || conta.Situacao == SituacaoContaBling.ParcialmentePago;
            if (!emAberto || conta.Vencimento.Date < inicio || conta.Vencimento.Date > fim)
            {
                continue;
            }

            string grupo = GruposFormaPagamento.Sem;
            if (conta.FormaPagamentoBlingId.HasValue)
            {
                string? guardado;
                if (grupos.TryGetValue(conta.EmpresaId + ":" + conta.FormaPagamentoBlingId.Value, out guardado))
                {
                    grupo = guardado;
                }
                else
                {
                    grupo = GruposFormaPagamento.Outras;
                }
            }

            TotalFormaPagamento? total;
            if (!porGrupo.TryGetValue(grupo, out total))
            {
                total = new TotalFormaPagamento();
                total.Grupo = grupo;
                total.Nome = GruposFormaPagamento.Nome(grupo);
                total.SaiSozinho = gruposSaiSozinho.Contains(grupo);
                porGrupo[grupo] = total;
            }
            total.Valor = total.Valor + conta.Valor;
            total.Quantidade = total.Quantidade + 1;
        }

        resumo.Formas = porGrupo.Values
            .OrderBy(f => f.SaiSozinho ? 1 : 0)
            .ThenBy(f => f.Grupo == GruposFormaPagamento.Sem ? 1 : 0)
            .ThenByDescending(f => f.Valor)
            .ThenBy(f => f.Nome)
            .ToList();
        resumo.TotalPrecisaPagar = resumo.Formas.Where(f => !f.SaiSozinho).Sum(f => f.Valor);
        resumo.QuantidadePrecisaPagar = resumo.Formas.Where(f => !f.SaiSozinho).Sum(f => f.Quantidade);
        resumo.TotalSaiSozinho = resumo.Formas.Where(f => f.SaiSozinho).Sum(f => f.Valor);
        resumo.QuantidadeSaiSozinho = resumo.Formas.Where(f => f.SaiSozinho).Sum(f => f.Quantidade);
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
