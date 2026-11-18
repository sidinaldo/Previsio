using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Relatorios;
using Previsio.Core.Entidades;
using Previsio.Core.Painel.Modelos;
using Xunit;

namespace Previsio.Tests.Aplicacao.Relatorios;

public class CalculoDossieTests
{
    private static readonly DateTime Hoje = new DateTime(2026, 11, 16);

    private static ResumoPainel Resumo(bool fluxo, params (decimal Entradas, decimal Saidas)[] semanas)
    {
        var resumo = new ResumoPainel();
        resumo.Inicio = Hoje;
        resumo.Fim = Hoje.AddDays(7 * semanas.Length - 1);
        resumo.FluxoCaixaAtivo = fluxo;
        resumo.TotalVencidas = 75m;
        resumo.TotalReceberVencido = 20m;
        for (int i = 0; i < semanas.Length; i++)
        {
            resumo.Semanas.Add(new TotalSemana
            {
                Inicio = Hoje.AddDays(7 * i), Fim = Hoje.AddDays(7 * i + 6), Entradas = semanas[i].Entradas, Valor = semanas[i].Saidas
            });
        }
        return resumo;
    }

    [Fact]
    public void Projecao_marca_pico_de_saidas_e_a_semana_em_que_o_saldo_fica_negativo()
    {
        // Média das saídas: 1000; fator 1,5: pico a partir de 1500
        ResumoPainel resumo = Resumo(true, (1000m, 500m), (200m, 2000m), (900m, 500m), (1500m, 1000m));

        ProjecaoCaixaDto projecao = CalculoDossie.Projecao(resumo, 1.5m);

        Assert.Equal(1000m, projecao.MediaSaidasSemana);
        Assert.Equal(new decimal[] { 500m, -1300m, -900m, -400m }, projecao.Semanas.Select(s => s.SaldoAcumulado).ToArray());
        Assert.Equal(new[] { false, true, false, false }, projecao.Semanas.Select(s => s.Critica).ToArray());
        Assert.Equal(new[] { CalculoDossie.MotivoPico }, projecao.Semanas[1].Motivos.ToArray());
        Assert.Equal(1, projecao.QuantidadeCriticas);
        // Fica negativo na 2ª semana e só ela é marcada
        Assert.Equal(new[] { false, true, false, false }, projecao.Semanas.Select(s => s.SaldoFicaNegativo).ToArray());
        Assert.Equal(Hoje.AddDays(7), projecao.SaldoNegativoEm);
        Assert.Equal(-1300m, projecao.MenorSaldoAcumulado);
        Assert.Equal(Hoje.AddDays(7), projecao.SemanaMenorSaldo);
        Assert.Equal(3600m, projecao.TotalEntradas);
        Assert.Equal(4000m, projecao.TotalSaidas);
        Assert.Equal(-400m, projecao.Saldo);
        Assert.Equal(75m, projecao.SaidasVencidas);
    }

    [Fact]
    public void Sem_fluxo_de_caixa_as_entradas_ficam_zeradas_e_nao_marca_saldo_negativo()
    {
        ResumoPainel resumo = Resumo(false, (999m, 100m), (0m, 100m), (0m, 400m));

        ProjecaoCaixaDto projecao = CalculoDossie.Projecao(resumo, 1.5m);

        Assert.Equal(0m, projecao.TotalEntradas);
        Assert.Equal(new[] { false, false, true }, projecao.Semanas.Select(s => s.Critica).ToArray());
        Assert.Equal(new[] { CalculoDossie.MotivoPico }, projecao.Semanas[2].Motivos.ToArray());
        Assert.Null(projecao.SaldoNegativoEm);
    }

    [Fact]
    public void Indicadores_e_indice_com_pesos_e_faixas()
    {
        DateTime conferida = DateTime.UtcNow;
        var contas = new List<ContaPagarSincronizada>
        {
            new ContaPagarSincronizada { Valor = 900m, CategoriaErpId = 1, CategoriaConferidaEm = conferida },
            new ContaPagarSincronizada { Valor = 100m, CategoriaConferidaEm = conferida },
            new ContaPagarSincronizada { Valor = 5000m }
        };
        IndicadorConformidadeDto categorias = CalculoDossie.Categorias(contas);
        Assert.Equal(90, categorias.Percentual);
        Assert.Equal(1, categorias.Quantidade);
        Assert.Equal(100m, categorias.Valor);
        Assert.Equal(1, categorias.QuantidadeAConferir);
        Assert.Equal("1 conta sem categoria no Bling", categorias.Descricao);

        var notas = new List<NotaFiscal>
        {
            new NotaFiscal { Status = StatusNota.Lancada, ValorTotal = 10m },
            new NotaFiscal { Status = StatusNota.LancadaManualmente, ValorTotal = 10m },
            new NotaFiscal { Status = StatusNota.Erro, ValorTotal = 300m },
            new NotaFiscal { Status = StatusNota.CanceladaNaSefaz, ValorTotal = 999m }
        };
        IndicadorConformidadeDto lancadas = CalculoDossie.Notas(notas);
        Assert.Equal(66, lancadas.Percentual);
        Assert.Equal(300m, lancadas.Valor);

        var abertas = new List<ContaPagarSincronizada>
        {
            new ContaPagarSincronizada { Valor = 200m, Vencimento = Hoje.AddDays(-1) },
            new ContaPagarSincronizada { Valor = 800m, Vencimento = Hoje }
        };
        IndicadorConformidadeDto emDia = CalculoDossie.EmDia(abertas, Hoje);
        Assert.Equal(80, emDia.Percentual);
        Assert.Equal(1, emDia.Quantidade);

        var auditoria = new AuditoriaLancamentosDto();
        categorias.Peso = 1;
        lancadas.Peso = 1;
        emDia.Peso = 2;
        auditoria.Indicadores.AddRange(new[] { categorias, lancadas, emDia });
        CalculoDossie.AplicarIndice(auditoria, 90, 70);

        // (90 + 66 + 80×2) / 4 = 79
        Assert.Equal(79, auditoria.Indice);
        Assert.Equal(CalculoDossie.FaixaAmarela, auditoria.Faixa);
        Assert.Equal(CalculoDossie.FaixaVerde, categorias.Faixa);
        Assert.Equal(CalculoDossie.FaixaVermelha, lancadas.Faixa);
    }

    [Fact]
    public void Sem_base_o_indicador_e_o_indice_ficam_nulos()
    {
        var auditoria = new AuditoriaLancamentosDto();
        IndicadorConformidadeDto notas = CalculoDossie.Notas(new List<NotaFiscal>());
        notas.Peso = 1;
        auditoria.Indicadores.Add(notas);
        CalculoDossie.AplicarIndice(auditoria, 90, 70);

        Assert.Null(notas.Percentual);
        Assert.Equal("Nenhuma nota ainda não lançada", notas.Descricao);
        Assert.Null(auditoria.Indice);
    }

    [Fact]
    public void Comparativo_pesa_as_empresas_e_ranqueia_fornecedores_e_categorias()
    {
        DateTime conferida = DateTime.UtcNow;
        var matriz = new Empresa { Id = 1, Nome = "Matriz", Cnpj = "11222333000181" };
        var filial = new Empresa { Id = 2, Nome = "Filial", Cnpj = "11222333000262" };
        var contas = new List<ContaPagarSincronizada>
        {
            new ContaPagarSincronizada { EmpresaId = 1, Valor = 600m, Situacao = SituacaoConta.Pago, FornecedorNome = "ALFA", CategoriaErpId = 7, CategoriaConferidaEm = conferida },
            new ContaPagarSincronizada { EmpresaId = 1, Valor = 150m, Situacao = SituacaoConta.EmAberto, FornecedorNome = "alfa ", CategoriaErpId = 8, CategoriaConferidaEm = conferida },
            new ContaPagarSincronizada { EmpresaId = 2, Valor = 100m, Situacao = SituacaoConta.EmAberto, FornecedorNome = "BETA", CategoriaErpId = 7, CategoriaConferidaEm = conferida },
            new ContaPagarSincronizada { EmpresaId = 2, Valor = 50m, Situacao = SituacaoConta.EmAberto, FornecedorNome = "GAMA", CategoriaConferidaEm = conferida },
            new ContaPagarSincronizada { EmpresaId = 2, Valor = 100m, Situacao = SituacaoConta.EmAberto }
        };
        var receber = new List<ContaReceberSincronizada> { new ContaReceberSincronizada { EmpresaId = 2, Valor = 400m } };
        var nomes = new Dictionary<string, string>
        {
            { "1:7", "Compras de Mercadorias" }, { "2:7", "compras de mercadorias" }, { "1:8", "Fretes" }
        };

        ComparativoDto comparativo = CalculoDossie.Comparativo(new List<Empresa> { matriz, filial }, contas, receber, nomes, 2);

        Assert.Equal(1000m, comparativo.TotalSaidas);
        Assert.Equal("Matriz", comparativo.Empresas[0].Nome);
        Assert.Equal(75m, comparativo.Empresas[0].Percentual);
        Assert.Equal(600m, comparativo.Empresas[0].SaidasPagas);
        Assert.Equal(150m, comparativo.Empresas[0].SaidasEmAberto);
        Assert.Equal(400m, comparativo.Empresas[1].Entradas);

        // ALFA soma sem diferença de maiúsculas; top 2 (empate pelo nome) e o resto em "Outros fornecedores"
        Assert.Equal(new[] { "ALFA", "BETA", CalculoDossie.OutrosFornecedores },
            comparativo.Fornecedores.Select(f => f.Nome).ToArray());
        Assert.Equal(750m, comparativo.Fornecedores[0].Valor);
        Assert.Equal(75m, comparativo.Fornecedores[0].Percentual);
        // GAMA e o fornecedor não identificado
        Assert.Equal(150m, comparativo.Fornecedores[2].Valor);
        Assert.Equal(2, comparativo.Fornecedores[2].Quantidade);

        Assert.Equal(new[] { "Compras de Mercadorias", "Fretes", "Sem categoria", "A conferir" },
            comparativo.Categorias.Select(c => c.Nome).ToArray());
        Assert.Equal(700m, comparativo.Categorias[0].Valor);
        Assert.Equal(5m, comparativo.Categorias[2].Percentual);
    }
}
