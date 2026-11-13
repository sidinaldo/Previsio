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

    private static ContaReceberSincronizada Receber(int empresaId, int dias, decimal valor, int situacao)
    {
        return new ContaReceberSincronizada { EmpresaId = empresaId, Vencimento = Hoje.AddDays(dias), Valor = valor, Situacao = situacao };
    }

    [Fact]
    public void Fluxo_de_caixa_soma_entradas_por_semana_e_calcula_o_saldo()
    {
        var empresas = new List<Empresa> { new Empresa { Id = 1, Nome = "Matriz" }, new Empresa { Id = 2, Nome = "Filial" } };
        var pagar = new List<ContaPagarSincronizada>
        {
            new ContaPagarSincronizada { EmpresaId = 1, Vencimento = Hoje.AddDays(1), Valor = 1000m, Situacao = SituacaoContaBling.EmAberto },
            new ContaPagarSincronizada { EmpresaId = 2, Vencimento = Hoje.AddDays(8), Valor = 300m, Situacao = SituacaoContaBling.EmAberto }
        };
        var receber = new List<ContaReceberSincronizada>
        {
            Receber(1, 2, 400m, SituacaoContaBling.EmAberto),
            Receber(2, 9, 900m, SituacaoContaBling.ParcialmentePago),
            Receber(1, 3, 50m, SituacaoContaBling.Pago),        // já recebida: fora
            Receber(1, -4, 700m, SituacaoContaBling.EmAberto),  // vencida: fora do saldo, card à parte
            Receber(1, 30, 999m, SituacaoContaBling.EmAberto)   // depois do período de 14 dias
        };
        var calculadora = new CalculadoraPainel();
        ResumoPainel resumo = calculadora.Calcular(Hoje, 14, empresas, pagar, new List<NotaFiscal>());

        calculadora.AplicarEntradas(resumo, receber);

        Assert.True(resumo.FluxoCaixaAtivo);
        Assert.Equal(1300m, resumo.TotalEntradasPeriodo);
        Assert.Equal(2, resumo.QuantidadeContasReceber);
        Assert.Equal(0m, resumo.SaldoPeriodo);
        Assert.Equal(700m, resumo.TotalReceberVencido);
        Assert.Equal(1, resumo.QuantidadeReceberVencido);
        Assert.Equal(400m, resumo.Semanas[0].Entradas);
        Assert.Equal(-600m, resumo.Semanas[0].Saldo);
        Assert.Equal(600m, resumo.Semanas[1].Saldo);
        Assert.Equal(0m, resumo.Semanas[1].SaldoAcumulado);
        Assert.Equal(400m, resumo.Empresas.Single(e => e.EmpresaId == 1).ValorReceber);
        Assert.Equal(900m, resumo.Empresas.Single(e => e.EmpresaId == 2).ValorReceber);
    }

    [Fact]
    public void Sem_aplicar_entradas_o_fluxo_de_caixa_fica_desligado()
    {
        ResumoPainel resumo = new CalculadoraPainel().Calcular(Hoje, 30, new List<Empresa>(), new List<ContaPagarSincronizada>(), new List<NotaFiscal>());

        Assert.False(resumo.FluxoCaixaAtivo);
        Assert.Equal(0m, resumo.Semanas[0].Entradas);
    }

    [Fact]
    public void Categorias_somam_as_seis_maiores_e_juntam_o_resto_em_outras()
    {
        var pagar = new List<ContaPagarSincronizada>();
        var nomes = new Dictionary<string, string>();
        for (int i = 1; i <= 8; i++)
        {
            ContaPagarSincronizada conta = Conta(1, i, i * 100m, SituacaoContaBling.EmAberto);
            conta.CategoriaBlingId = i;
            conta.CategoriaConferidaEm = Hoje;
            pagar.Add(conta);
            nomes["1:" + i] = "Categoria " + i;
        }
        // A mesma categoria (pelo nome) em outra empresa soma junto
        ContaPagarSincronizada outraEmpresa = Conta(2, 2, 1000m, SituacaoContaBling.EmAberto);
        outraEmpresa.CategoriaBlingId = 50;
        outraEmpresa.CategoriaConferidaEm = Hoje;
        pagar.Add(outraEmpresa);
        nomes["2:50"] = "CATEGORIA 1";
        ContaPagarSincronizada sem = Conta(1, 3, 70m, SituacaoContaBling.EmAberto);
        sem.CategoriaConferidaEm = Hoje;
        pagar.Add(sem);
        pagar.Add(Conta(1, 4, 30m, SituacaoContaBling.EmAberto));
        // Paga e fora do período não entram
        ContaPagarSincronizada paga = Conta(1, 1, 9999m, SituacaoContaBling.Pago);
        paga.CategoriaBlingId = 1;
        pagar.Add(paga);
        ContaPagarSincronizada longe = Conta(1, 60, 9999m, SituacaoContaBling.EmAberto);
        longe.CategoriaBlingId = 1;
        pagar.Add(longe);

        var calculadora = new CalculadoraPainel();
        ResumoPainel resumo = calculadora.Calcular(Hoje, 30, new List<Empresa>(), pagar, new List<NotaFiscal>());
        calculadora.AplicarCategorias(resumo, pagar, nomes);

        Assert.Equal(9, resumo.Categorias.Count);
        Assert.Equal("Categoria 1", resumo.Categorias[0].Nome);
        Assert.Equal(1100m, resumo.Categorias[0].Valor);
        Assert.Equal(2, resumo.Categorias[0].Quantidade);
        Assert.Equal("Categoria 8", resumo.Categorias[1].Nome);
        Assert.Equal("Outras (2)", resumo.Categorias[6].Nome);
        Assert.Equal(TotalCategoria.TipoOutras, resumo.Categorias[6].Tipo);
        Assert.Equal(500m, resumo.Categorias[6].Valor);
        Assert.Equal(TotalCategoria.TipoSem, resumo.Categorias[7].Tipo);
        Assert.Equal(70m, resumo.Categorias[7].Valor);
        Assert.Equal(TotalCategoria.TipoAConferir, resumo.Categorias[8].Tipo);
        Assert.Equal(30m, resumo.Categorias[8].Valor);
    }

    [Fact]
    public void Categoria_fora_da_lista_da_empresa_vai_para_nao_identificada()
    {
        ContaPagarSincronizada conhecida = Conta(1, 2, 100m, SituacaoContaBling.EmAberto);
        conhecida.CategoriaBlingId = 1;
        conhecida.CategoriaConferidaEm = Hoje;
        ContaPagarSincronizada desconhecida = Conta(1, 3, 40m, SituacaoContaBling.EmAberto);
        desconhecida.CategoriaBlingId = 99;
        desconhecida.CategoriaConferidaEm = Hoje;
        ContaPagarSincronizada sem = Conta(1, 4, 10m, SituacaoContaBling.EmAberto);
        sem.CategoriaConferidaEm = Hoje;
        var pagar = new List<ContaPagarSincronizada> { conhecida, desconhecida, sem };
        var nomes = new Dictionary<string, string> { { "1:1", "Compras" } };

        var calculadora = new CalculadoraPainel();
        ResumoPainel resumo = calculadora.Calcular(Hoje, 30, new List<Empresa>(), pagar, new List<NotaFiscal>());
        calculadora.AplicarCategorias(resumo, pagar, nomes);

        Assert.Equal(new[] { "Compras", "Sem categoria", TotalCategoria.NomeNaoIdentificada }, resumo.Categorias.Select(c => c.Nome).ToArray());
        Assert.Equal(TotalCategoria.TipoNaoIdentificada, resumo.Categorias[2].Tipo);
        Assert.Equal(40m, resumo.Categorias[2].Valor);
    }
}
