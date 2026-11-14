using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Exportacao;
using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Relatorios;

/// <summary>
/// As abas da planilha do Dossiê financeiro: os números das tabelas do relatório e as contas do período.
/// </summary>
public static class PlanilhaDossie
{
    public static PlanilhaXlsx Projecao(ProjecaoCaixaDto projecao)
    {
        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Semana de", TipoColunaPlanilha.Data, 12),
            new ColunaPlanilha("Até", TipoColunaPlanilha.Data, 12),
            new ColunaPlanilha("Entradas", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Saídas", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Saldo da semana", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Saldo acumulado", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Atenção", TipoColunaPlanilha.Texto, 34)
        };
        var planilha = new PlanilhaXlsx("Projeção", colunas);
        foreach (SemanaProjecaoDto semana in projecao.Semanas)
        {
            string critica = "";
            if (semana.Critica)
            {
                critica = string.Join(" e ", semana.Motivos);
            }
            if (semana.SaldoFicaNegativo)
            {
                if (critica != "")
                {
                    critica = critica + " e ";
                }
                critica = critica + "saldo fica negativo";
            }
            planilha.AdicionarLinha(semana.Inicio, semana.Fim, semana.Entradas, semana.Saidas, semana.Saldo, semana.SaldoAcumulado, critica);
        }
        planilha.AdicionarLinha(null, null, projecao.TotalEntradas, projecao.TotalSaidas, projecao.Saldo, null, "Total do período");
        return planilha;
    }

    public static PlanilhaXlsx Auditoria(AuditoriaLancamentosDto auditoria)
    {
        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Indicador", TipoColunaPlanilha.Texto, 30),
            new ColunaPlanilha("Em ordem (%)", TipoColunaPlanilha.Numero, 14),
            new ColunaPlanilha("Faixa", TipoColunaPlanilha.Texto, 12),
            new ColunaPlanilha("A arrumar", TipoColunaPlanilha.Texto, 40),
            new ColunaPlanilha("Valor a arrumar", TipoColunaPlanilha.Dinheiro, 18),
            new ColunaPlanilha("Peso no índice", TipoColunaPlanilha.Numero, 14)
        };
        var planilha = new PlanilhaXlsx("Auditoria", colunas);
        foreach (IndicadorConformidadeDto indicador in auditoria.Indicadores)
        {
            planilha.AdicionarLinha(indicador.Nome, indicador.Percentual, indicador.Faixa, indicador.Descricao, indicador.Valor, indicador.Peso);
        }
        planilha.AdicionarLinha("Índice de conformidade", auditoria.Indice, auditoria.Faixa, null, null, null);
        if (auditoria.QuantidadeReceberVencido > 0)
        {
            planilha.AdicionarLinha("A receber vencido (fora do índice)", null, null,
                auditoria.QuantidadeReceberVencido + " contas a receber vencidas", auditoria.ValorReceberVencido, null);
        }
        return planilha;
    }

    public static PlanilhaXlsx Empresas(ComparativoDto comparativo)
    {
        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Empresa", TipoColunaPlanilha.Texto, 30),
            new ColunaPlanilha("CNPJ", TipoColunaPlanilha.Texto, 20),
            new ColunaPlanilha("Saídas", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Pagas", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Em aberto", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("% do grupo", TipoColunaPlanilha.Numero, 12),
            new ColunaPlanilha("Contas", TipoColunaPlanilha.Numero, 10),
            new ColunaPlanilha("Entradas", TipoColunaPlanilha.Dinheiro, 16)
        };
        var planilha = new PlanilhaXlsx("Por empresa", colunas);
        foreach (EmpresaComparativoDto empresa in comparativo.Empresas)
        {
            planilha.AdicionarLinha(empresa.Nome, Cnpj(empresa.Cnpj), empresa.Saidas, empresa.SaidasPagas, empresa.SaidasEmAberto,
                empresa.Percentual, empresa.QuantidadeContas, empresa.Entradas);
        }
        planilha.AdicionarLinha("Total", null, comparativo.TotalSaidas, comparativo.Empresas.Sum(e => e.SaidasPagas),
            comparativo.Empresas.Sum(e => e.SaidasEmAberto), null, comparativo.QuantidadeContas, comparativo.TotalEntradas);
        return planilha;
    }

    public static PlanilhaXlsx Ranking(string aba, string titulo, List<ItemRankingDto> itens)
    {
        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha(titulo, TipoColunaPlanilha.Texto, 40),
            new ColunaPlanilha("Saídas", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("% das saídas", TipoColunaPlanilha.Numero, 14),
            new ColunaPlanilha("Contas", TipoColunaPlanilha.Numero, 10)
        };
        var planilha = new PlanilhaXlsx(aba, colunas);
        foreach (ItemRankingDto item in itens)
        {
            planilha.AdicionarLinha(item.Nome, item.Valor, item.Percentual, item.Quantidade);
        }
        return planilha;
    }

    /// <summary>Dados brutos: as contas a pagar do período, para filtrar e somar no Excel.</summary>
    public static PlanilhaXlsx Contas(
        List<ContaPagarSincronizada> contas, Dictionary<int, string> empresas, Dictionary<string, string> categorias)
    {
        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Vencimento", TipoColunaPlanilha.Data, 12),
            new ColunaPlanilha("Empresa", TipoColunaPlanilha.Texto, 28),
            new ColunaPlanilha("Fornecedor", TipoColunaPlanilha.Texto, 36),
            new ColunaPlanilha("Categoria", TipoColunaPlanilha.Texto, 28),
            new ColunaPlanilha("Situação", TipoColunaPlanilha.Texto, 18),
            new ColunaPlanilha("Valor", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Conta no Bling", TipoColunaPlanilha.Numero, 16)
        };
        var planilha = new PlanilhaXlsx("Contas do período", colunas);
        foreach (ContaPagarSincronizada conta in contas)
        {
            string? empresa;
            empresas.TryGetValue(conta.EmpresaId, out empresa);
            string categoria;
            if (!conta.CategoriaConferidaEm.HasValue)
            {
                categoria = "A conferir";
            }
            else if (!conta.CategoriaBlingId.HasValue)
            {
                categoria = "Sem categoria";
            }
            else
            {
                string? nome;
                if (categorias.TryGetValue(conta.EmpresaId + ":" + conta.CategoriaBlingId.Value, out nome))
                {
                    categoria = nome;
                }
                else
                {
                    categoria = "Categoria não identificada";
                }
            }
            planilha.AdicionarLinha(conta.Vencimento, empresa, conta.FornecedorNome, categoria, TextoSituacaoConta.De(conta.Situacao),
                conta.Valor, conta.BlingId);
        }
        return planilha;
    }

    private static string Cnpj(string documento)
    {
        if (documento == null || documento.Length != 14)
        {
            return documento ?? "";
        }
        return documento.Substring(0, 2) + "." + documento.Substring(2, 3) + "." + documento.Substring(5, 3) + "/"
            + documento.Substring(8, 4) + "-" + documento.Substring(12, 2);
    }
}
