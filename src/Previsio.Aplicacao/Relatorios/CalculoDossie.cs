using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Notas;
using Previsio.Core.Entidades;
using Previsio.Core.Painel.Modelos;

namespace Previsio.Aplicacao.Relatorios;

/// <summary>
/// Contas do Dossiê financeiro, sem banco: projeção com as semanas críticas, indicadores de
/// conformidade e o comparativo por empresa, fornecedor e categoria.
/// </summary>
public static class CalculoDossie
{
    public const string MotivoPico = "Pico de saídas";
    public const string FaixaVerde = "verde";
    public const string FaixaAmarela = "amarelo";
    public const string FaixaVermelha = "vermelho";
    public const string FornecedorNaoIdentificado = "Fornecedor não identificado";
    public const string OutrosFornecedores = "Outros fornecedores";
    public const string OutrasCategorias = "Outras categorias";
    public const string TipoFornecedor = "fornecedor";

    /// <summary>
    /// As semanas do painel com o saldo acumulado a partir de zero. Crítica: pico de desembolso, com saídas
    /// a partir de "fator" vezes a média semanal. Com o fluxo de caixa ligado, marca também a semana em que
    /// o saldo acumulado fica negativo pela primeira vez.
    /// </summary>
    public static ProjecaoCaixaDto Projecao(ResumoPainel resumo, decimal fator)
    {
        var projecao = new ProjecaoCaixaDto();
        projecao.Inicio = resumo.Inicio;
        projecao.Fim = resumo.Fim;
        projecao.FluxoCaixaAtivo = resumo.FluxoCaixaAtivo;
        projecao.FatorSemanaCritica = fator;
        projecao.SaidasVencidas = resumo.TotalVencidas;
        projecao.EntradasVencidas = resumo.TotalReceberVencido;

        decimal totalSaidas = resumo.Semanas.Sum(s => s.Valor);
        decimal media = 0m;
        if (resumo.Semanas.Count > 0)
        {
            media = Math.Round(totalSaidas / resumo.Semanas.Count, 2);
        }
        projecao.MediaSaidasSemana = media;

        decimal acumulado = 0m;
        bool primeira = true;
        foreach (TotalSemana semana in resumo.Semanas)
        {
            var item = new SemanaProjecaoDto();
            item.Inicio = semana.Inicio;
            item.Fim = semana.Fim;
            item.Saidas = semana.Valor;
            if (resumo.FluxoCaixaAtivo)
            {
                item.Entradas = semana.Entradas;
            }
            item.Saldo = item.Entradas - item.Saidas;
            acumulado = acumulado + item.Saldo;
            item.SaldoAcumulado = acumulado;

            if (item.Saidas > 0m && media > 0m && item.Saidas >= media * fator)
            {
                item.Motivos.Add(MotivoPico);
            }
            item.Critica = item.Motivos.Count > 0;
            if (resumo.FluxoCaixaAtivo && item.SaldoAcumulado < 0m && !projecao.SaldoNegativoEm.HasValue)
            {
                projecao.SaldoNegativoEm = item.Inicio;
                item.SaldoFicaNegativo = true;
            }

            if (primeira || item.SaldoAcumulado < projecao.MenorSaldoAcumulado)
            {
                projecao.MenorSaldoAcumulado = item.SaldoAcumulado;
                projecao.SemanaMenorSaldo = item.Inicio;
            }
            primeira = false;
            projecao.Semanas.Add(item);
        }

        projecao.TotalSaidas = totalSaidas;
        projecao.TotalEntradas = projecao.Semanas.Sum(s => s.Entradas);
        projecao.Saldo = projecao.TotalEntradas - projecao.TotalSaidas;
        projecao.QuantidadeCriticas = projecao.Semanas.Count(s => s.Critica);
        return projecao;
    }

    /// <summary>
    /// Categorias: das contas a pagar do período já conferidas no Bling, quanto (em valor) tem categoria.
    /// </summary>
    public static IndicadorConformidadeDto Categorias(IList<ContaPagarSincronizada> contasPeriodo)
    {
        var indicador = new IndicadorConformidadeDto();
        indicador.Chave = "categorias";
        indicador.Nome = "Contas com categoria";
        List<ContaPagarSincronizada> conferidas = contasPeriodo.Where(c => c.CategoriaConferidaEm.HasValue).ToList();
        List<ContaPagarSincronizada> sem = conferidas.Where(c => !c.CategoriaBlingId.HasValue).ToList();
        indicador.Quantidade = sem.Count;
        indicador.Valor = sem.Sum(c => c.Valor);
        indicador.QuantidadeAConferir = contasPeriodo.Count - conferidas.Count;
        decimal base_ = conferidas.Sum(c => c.Valor);
        indicador.Percentual = Percentual(base_ - indicador.Valor, base_);
        indicador.Descricao = Plural(sem.Count, "conta sem categoria", "contas sem categoria") + " no Bling";
        return indicador;
    }

    /// <summary>
    /// Notas: das notas recebidas no período (sem as canceladas na SEFAZ), quantas já viraram conta no Bling.
    /// </summary>
    public static IndicadorConformidadeDto Notas(IList<NotaFiscal> notasPeriodo)
    {
        var indicador = new IndicadorConformidadeDto();
        indicador.Chave = "notas";
        indicador.Nome = "Notas lançadas no Bling";
        List<NotaFiscal> validas = notasPeriodo.Where(n => n.Status != StatusNota.CanceladaNaSefaz).ToList();
        List<NotaFiscal> pendentes = validas.Where(n => StatusPendentes.Todos.Contains(n.Status)).ToList();
        indicador.Quantidade = pendentes.Count;
        indicador.Valor = pendentes.Sum(n => n.ValorTotal);
        indicador.Percentual = Percentual(validas.Count - pendentes.Count, validas.Count);
        indicador.Descricao = Plural(pendentes.Count, "nota ainda não lançada", "notas ainda não lançadas");
        return indicador;
    }

    /// <summary>
    /// Em dia: do valor a pagar em aberto (de qualquer data), quanto não está vencido.
    /// </summary>
    public static IndicadorConformidadeDto EmDia(IList<ContaPagarSincronizada> emAberto, DateTime hoje)
    {
        var indicador = new IndicadorConformidadeDto();
        indicador.Chave = "em-dia";
        indicador.Nome = "Contas a pagar em dia";
        List<ContaPagarSincronizada> vencidas = emAberto.Where(c => c.Vencimento.Date < hoje.Date).ToList();
        indicador.Quantidade = vencidas.Count;
        indicador.Valor = vencidas.Sum(c => c.Valor);
        decimal total = emAberto.Sum(c => c.Valor);
        indicador.Percentual = Percentual(total - indicador.Valor, total);
        indicador.Descricao = Plural(vencidas.Count, "conta vencida em aberto", "contas vencidas em aberto");
        return indicador;
    }

    /// <summary>
    /// Índice: média dos percentuais pelos pesos, só dos indicadores com base e peso; nulo se nenhum tem.
    /// </summary>
    public static void AplicarIndice(AuditoriaLancamentosDto auditoria, int verde, int amarela)
    {
        auditoria.FaixaVerde = verde;
        auditoria.FaixaAmarela = amarela;
        decimal soma = 0m;
        int pesos = 0;
        foreach (IndicadorConformidadeDto indicador in auditoria.Indicadores)
        {
            if (indicador.Percentual.HasValue)
            {
                indicador.Faixa = Faixa(indicador.Percentual.Value, verde, amarela);
                if (indicador.Peso > 0)
                {
                    soma = soma + indicador.Percentual.Value * indicador.Peso;
                    pesos = pesos + indicador.Peso;
                }
            }
        }
        if (pesos == 0)
        {
            auditoria.Indice = null;
            auditoria.Faixa = "";
            return;
        }
        auditoria.Indice = (int)Math.Floor(soma / pesos);
        auditoria.Faixa = Faixa(auditoria.Indice.Value, verde, amarela);
    }

    public static string Faixa(int percentual, int verde, int amarela)
    {
        if (percentual >= verde)
        {
            return FaixaVerde;
        }
        if (percentual >= amarela)
        {
            return FaixaAmarela;
        }
        return FaixaVermelha;
    }

    /// <summary>
    /// Comparativo do período com as contas a pagar (pagas e em aberto, sem devolvidas e canceladas)
    /// e as contas a receber do período. "nomes" leva EmpresaId:BlingId ao nome da categoria.
    /// </summary>
    public static ComparativoDto Comparativo(
        IList<Empresa> empresas, IList<ContaPagarSincronizada> contas, IList<ContaReceberSincronizada> receber,
        IDictionary<string, string> nomes, int tamanhoRanking)
    {
        var comparativo = new ComparativoDto();
        comparativo.TotalSaidas = contas.Sum(c => c.Valor);
        comparativo.TotalEntradas = receber.Sum(c => c.Valor);
        comparativo.QuantidadeContas = contas.Count;

        foreach (Empresa empresa in empresas)
        {
            List<ContaPagarSincronizada> daEmpresa = contas.Where(c => c.EmpresaId == empresa.Id).ToList();
            var item = new EmpresaComparativoDto();
            item.EmpresaId = empresa.Id;
            item.Nome = empresa.Nome;
            item.Cnpj = empresa.Cnpj;
            item.Saidas = daEmpresa.Sum(c => c.Valor);
            item.SaidasPagas = daEmpresa.Where(c => c.Situacao == SituacaoContaBling.Pago).Sum(c => c.Valor);
            item.SaidasEmAberto = item.Saidas - item.SaidasPagas;
            item.Entradas = receber.Where(c => c.EmpresaId == empresa.Id).Sum(c => c.Valor);
            item.QuantidadeContas = daEmpresa.Count;
            item.Percentual = Parte(item.Saidas, comparativo.TotalSaidas);
            comparativo.Empresas.Add(item);
        }
        comparativo.Empresas = comparativo.Empresas.OrderByDescending(e => e.Saidas).ThenBy(e => e.Nome).ToList();

        // Fornecedores: pelo nome, sem diferença de maiúsculas
        var fornecedores = new Dictionary<string, ItemRankingDto>(StringComparer.OrdinalIgnoreCase);
        foreach (ContaPagarSincronizada conta in contas)
        {
            string nome = (conta.FornecedorNome ?? "").Trim();
            if (nome == "")
            {
                nome = FornecedorNaoIdentificado;
            }
            Somar(fornecedores, nome, TipoFornecedor, conta.Valor);
        }
        comparativo.Fornecedores = Ranking(fornecedores.Values, tamanhoRanking, OutrosFornecedores, comparativo.TotalSaidas);

        // Categorias: pelo nome no Bling de cada empresa; sem, a conferir e não identificada à parte
        var categorias = new Dictionary<string, ItemRankingDto>(StringComparer.OrdinalIgnoreCase);
        var especiais = new Dictionary<string, ItemRankingDto>();
        foreach (ContaPagarSincronizada conta in contas)
        {
            if (!conta.CategoriaConferidaEm.HasValue)
            {
                Somar(especiais, "A conferir", TotalCategoria.TipoAConferir, conta.Valor);
            }
            else if (!conta.CategoriaBlingId.HasValue)
            {
                Somar(especiais, "Sem categoria", TotalCategoria.TipoSem, conta.Valor);
            }
            else
            {
                string? nome;
                if (nomes.TryGetValue(conta.EmpresaId + ":" + conta.CategoriaBlingId.Value, out nome))
                {
                    Somar(categorias, nome.Trim(), TotalCategoria.TipoCategoria, conta.Valor);
                }
                else
                {
                    Somar(especiais, TotalCategoria.NomeNaoIdentificada, TotalCategoria.TipoNaoIdentificada, conta.Valor);
                }
            }
        }
        comparativo.Categorias = Ranking(categorias.Values, tamanhoRanking, OutrasCategorias, comparativo.TotalSaidas);
        foreach (string tipo in new[] { TotalCategoria.TipoSem, TotalCategoria.TipoAConferir, TotalCategoria.TipoNaoIdentificada })
        {
            ItemRankingDto? especial = especiais.Values.FirstOrDefault(e => e.Tipo == tipo);
            if (especial != null)
            {
                especial.Percentual = Parte(especial.Valor, comparativo.TotalSaidas);
                comparativo.Categorias.Add(especial);
            }
        }
        return comparativo;
    }

    private static void Somar(Dictionary<string, ItemRankingDto> itens, string nome, string tipo, decimal valor)
    {
        ItemRankingDto? item;
        if (!itens.TryGetValue(nome, out item))
        {
            item = new ItemRankingDto();
            item.Nome = nome;
            item.Tipo = tipo;
            itens[nome] = item;
        }
        item.Valor = item.Valor + valor;
        item.Quantidade = item.Quantidade + 1;
    }

    // Os "tamanho" maiores e o resto somado em "nomeResto"
    private static List<ItemRankingDto> Ranking(IEnumerable<ItemRankingDto> itens, int tamanho, string nomeResto, decimal total)
    {
        List<ItemRankingDto> ordenados = itens.OrderByDescending(i => i.Valor).ThenBy(i => i.Nome).ToList();
        List<ItemRankingDto> ranking = ordenados.Take(tamanho).ToList();
        List<ItemRankingDto> resto = ordenados.Skip(tamanho).ToList();
        if (resto.Count > 0)
        {
            var outros = new ItemRankingDto();
            outros.Nome = nomeResto;
            outros.Tipo = "outros";
            outros.Valor = resto.Sum(i => i.Valor);
            outros.Quantidade = resto.Sum(i => i.Quantidade);
            ranking.Add(outros);
        }
        foreach (ItemRankingDto item in ranking)
        {
            item.Percentual = Parte(item.Valor, total);
        }
        return ranking;
    }

    // Percentual para baixo: 99,6% fica 99, para não esconder o que falta arrumar
    private static int? Percentual(decimal parte, decimal total)
    {
        if (total <= 0m)
        {
            return null;
        }
        return (int)Math.Floor(parte * 100m / total);
    }

    private static decimal Parte(decimal valor, decimal total)
    {
        if (total <= 0m)
        {
            return 0m;
        }
        return Math.Round(valor * 100m / total, 1);
    }

    private static string Plural(int quantidade, string singular, string plural)
    {
        if (quantidade == 0)
        {
            return "Nenhuma " + singular;
        }
        if (quantidade == 1)
        {
            return "1 " + singular;
        }
        return quantidade + " " + plural;
    }
}
