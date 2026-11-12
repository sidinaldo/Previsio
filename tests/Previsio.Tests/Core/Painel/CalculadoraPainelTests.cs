using Previsio.Core.Entidades;
using Previsio.Core.Painel;
using Previsio.Core.Painel.Modelos;
using Xunit;

namespace Previsio.Tests.Core.Painel;

public class CalculadoraPainelTests
{
    private static readonly DateTime Hoje = new DateTime(2026, 11, 10);

    private static ContaPagarSincronizada Conta(int empresaId, int diasAPartirDeHoje, decimal valor, int situacao)
    {
        var conta = new ContaPagarSincronizada();
        conta.EmpresaId = empresaId;
        conta.Vencimento = Hoje.AddDays(diasAPartirDeHoje);
        conta.Valor = valor;
        conta.Situacao = situacao;
        return conta;
    }

    [Fact]
    public void Soma_somente_contas_em_aberto_dentro_do_periodo()
    {
        var empresas = new List<Empresa>
        {
            new Empresa { Id = 1, Nome = "Matriz" },
            new Empresa { Id = 2, Nome = "Filial" }
        };

        var contas = new List<ContaPagarSincronizada>
        {
            Conta(1, 0, 100m, SituacaoContaBling.EmAberto),
            Conta(1, 8, 200m, SituacaoContaBling.EmAberto),
            Conta(2, 3, 50m, SituacaoContaBling.ParcialmentePago),
            Conta(2, 5, 999m, SituacaoContaBling.Pago),       // já paga: fora
            Conta(1, 40, 999m, SituacaoContaBling.EmAberto),  // fora dos 30 dias
            Conta(1, -1, 999m, SituacaoContaBling.EmAberto)   // vencida ontem: fora
        };

        var calculadora = new CalculadoraPainel();
        ResumoPainel resumo = calculadora.Calcular(Hoje, 30, empresas, contas, new List<NotaFiscal>());

        Assert.Equal(350m, resumo.TotalPeriodo);
        Assert.Equal(3, resumo.QuantidadeContas);
        Assert.Equal(150m, resumo.TotalSemanaAtual);
        Assert.Equal(300m, resumo.Empresas.Single(e => e.EmpresaId == 1).Valor);
        Assert.Equal(50m, resumo.Empresas.Single(e => e.EmpresaId == 2).Valor);
    }

    [Fact]
    public void Semanas_cobrem_todo_o_periodo_sem_sobrepor()
    {
        var calculadora = new CalculadoraPainel();
        ResumoPainel resumo = calculadora.Calcular(
            Hoje, 30, new List<Empresa>(), new List<ContaPagarSincronizada>(), new List<NotaFiscal>());

        Assert.Equal(5, resumo.Semanas.Count);
        Assert.Equal(Hoje, resumo.Semanas[0].Inicio);
        Assert.Equal(Hoje.AddDays(29), resumo.Semanas[4].Fim);

        for (int i = 1; i < resumo.Semanas.Count; i++)
        {
            Assert.Equal(resumo.Semanas[i - 1].Fim.AddDays(1), resumo.Semanas[i].Inicio);
        }
    }

    [Fact]
    public void Conta_notas_pendentes_por_empresa()
    {
        var empresas = new List<Empresa> { new Empresa { Id = 1, Nome = "Matriz" } };
        var pendentes = new List<NotaFiscal>
        {
            new NotaFiscal { EmpresaId = 1, ValorTotal = 1000m },
            new NotaFiscal { EmpresaId = 1, ValorTotal = 500m },
            new NotaFiscal { EmpresaId = null, ValorTotal = 200m }
        };

        var calculadora = new CalculadoraPainel();
        ResumoPainel resumo = calculadora.Calcular(Hoje, 30, empresas, new List<ContaPagarSincronizada>(), pendentes);

        Assert.Equal(3, resumo.NotasPendentes);
        Assert.Equal(1700m, resumo.ValorNotasPendentes);
        Assert.Equal(2, resumo.Empresas[0].NotasPendentes);
        Assert.Equal(1500m, resumo.Empresas[0].ValorNotasPendentes);
    }

    private static NotaFiscal Pendente(int id, int? empresaId, params int[] diasDasParcelas)
    {
        var nota = new NotaFiscal { Id = id, EmpresaId = empresaId, Numero = id.ToString(), NomeFornecedor = "Fornecedor " + id, ValorTotal = 100m * id, Status = StatusNota.Erro };
        foreach (int dias in diasDasParcelas)
        {
            nota.Parcelas.Add(new ParcelaNota { Vencimento = Hoje.AddDays(dias), Valor = 10m });
        }
        return nota;
    }

    [Fact]
    public void Notas_para_resolver_vem_pela_primeira_parcela_e_param_em_cinco()
    {
        var empresas = new List<Empresa> { new Empresa { Id = 1, Nome = "Matriz" } };
        var pendentes = new List<NotaFiscal>
        {
            Pendente(1, 1, 20),
            Pendente(2, 1),              // sem parcelas: vai para o fim
            Pendente(3, null, 5, -2),    // já venceu uma parcela: primeira da lista
            Pendente(4, 1, 7),
            Pendente(5, 1, 7),           // mesmo dia da 4: ordem de chegada
            Pendente(6, 1, 30),
            Pendente(7, 1, 40)
        };

        ResumoPainel resumo = new CalculadoraPainel().Calcular(Hoje, 30, empresas, new List<ContaPagarSincronizada>(), pendentes);

        Assert.Equal(7, resumo.NotasPendentes);
        Assert.Equal(new[] { 3, 4, 5, 1, 6 }, resumo.NotasParaResolver.Select(n => n.NotaId).ToArray());
        NotaParaResolver primeira = resumo.NotasParaResolver[0];
        Assert.Equal(Hoje.AddDays(-2), primeira.PrimeiroVencimento);
        Assert.Null(primeira.EmpresaNome);
        Assert.Equal("Erro", primeira.Status);
        Assert.Equal("Matriz", resumo.NotasParaResolver[1].EmpresaNome);
    }

    [Fact]
    public void Nota_sem_parcelas_aparece_sem_vencimento_quando_cabe_na_lista()
    {
        var pendentes = new List<NotaFiscal> { Pendente(2, 1), Pendente(1, 1, 3) };

        ResumoPainel resumo = new CalculadoraPainel().Calcular(Hoje, 30, new List<Empresa>(), new List<ContaPagarSincronizada>(), pendentes);

        Assert.Equal(new[] { 1, 2 }, resumo.NotasParaResolver.Select(n => n.NotaId).ToArray());
        Assert.Null(resumo.NotasParaResolver[1].PrimeiroVencimento);
    }
}
