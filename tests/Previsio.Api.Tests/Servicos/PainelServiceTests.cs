using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Core.Painel.Modelos;
using Previsio.Data;
using Xunit;

namespace Previsio.Api.Tests.Servicos;

public class PainelServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();

    private async Task<Empresa> CadastrarEmpresa(int clienteId, string nome, bool ativa)
    {
        var empresa = new Empresa();
        empresa.ClienteId = clienteId;
        empresa.Nome = nome;
        empresa.Cnpj = "1122233300018" + (_db.Empresas.Count() + 1);
        empresa.Ativo = ativa;
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa;
    }

    private void Conta(int empresaId, long blingId, DateTime vencimento, decimal valor, int situacao)
    {
        var conta = new ContaPagarSincronizada();
        conta.EmpresaId = empresaId;
        conta.BlingId = blingId;
        conta.Vencimento = vencimento;
        conta.Valor = valor;
        conta.Situacao = situacao;
        _db.ContasPagar.Add(conta);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(-5, 30)]
    [InlineData(121, 30)]
    [InlineData(45, 45)]
    [InlineData(120, 120)]
    public void NormalizarDias_ForaDoIntervaloUsaOPadrao(int informado, int esperado)
    {
        Assert.Equal(esperado, PainelService.NormalizarDias(informado));
    }

    [Fact]
    public async Task Vencimentos_SoContasEmAbertoAgrupadasPorDia()
    {
        Empresa empresa = await CadastrarEmpresa(1, "Matriz", true);
        DateTime amanha = RelogioBrasil.Hoje().AddDays(1);
        Conta(empresa.Id, 1, amanha, 100m, SituacaoContaBling.EmAberto);
        Conta(empresa.Id, 2, amanha, 50m, SituacaoContaBling.ParcialmentePago);
        Conta(empresa.Id, 3, amanha, 999m, SituacaoContaBling.Pago);
        Conta(empresa.Id, 4, amanha.AddDays(2), 30m, SituacaoContaBling.EmAberto);
        Conta(empresa.Id, 5, amanha.AddDays(60), 70m, SituacaoContaBling.EmAberto);
        await _db.SaveChangesAsync();

        List<VencimentoDiaDto> dias = await ServicosDeTeste.Painel(_db).VencimentosAsync(empresa.Id, 30, CancellationToken.None);

        Assert.Equal(2, dias.Count);
        Assert.Equal(amanha, dias[0].Data);
        Assert.Equal(2, dias[0].Quantidade);
        Assert.Equal(150m, dias[0].Valor);
        Assert.Equal(30m, dias[1].Valor);
    }

    [Fact]
    public async Task Resumo_IgnoraEmpresasInativasEOutrosClientes()
    {
        Empresa ativa = await CadastrarEmpresa(1, "Matriz", true);
        Empresa inativa = await CadastrarEmpresa(1, "Filial antiga", false);
        Empresa outroCliente = await CadastrarEmpresa(2, "Outra rede", true);
        DateTime amanha = RelogioBrasil.Hoje().AddDays(1);
        Conta(ativa.Id, 1, amanha, 100m, SituacaoContaBling.EmAberto);
        Conta(inativa.Id, 2, amanha, 200m, SituacaoContaBling.EmAberto);
        Conta(outroCliente.Id, 3, amanha, 400m, SituacaoContaBling.EmAberto);
        await _db.SaveChangesAsync();

        ResumoPainel resumo = await ServicosDeTeste.Painel(_db).ResumoAsync(1, 30, CancellationToken.None);

        Assert.Single(resumo.Empresas);
        Assert.Equal(100m, resumo.TotalPeriodo);
    }
}
