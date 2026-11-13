using Microsoft.Extensions.Options;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.Contas;

public class ContasReceberServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    // 11/11/2026 09h em Brasília
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();

    private Cliente _rede = null!;
    private Empresa _centro = null!;
    private Empresa _norte = null!;

    private ContasReceberService CriarServico()
    {
        return ServicosDeTeste.ContasReceberService(_db, _relogio, Options.Create(new SincronizacaoOptions()));
    }

    private async Task CadastrarRedes()
    {
        _rede = new Cliente { Nome = "Rede" };
        var outra = new Cliente { Nome = "Outra rede" };
        _db.Clientes.Add(_rede);
        _db.Clientes.Add(outra);
        await _db.SaveChangesAsync();
        _centro = new Empresa { ClienteId = _rede.Id, Nome = "Auto Center Centro", Cnpj = "11222333000262" };
        _norte = new Empresa { ClienteId = _rede.Id, Nome = "Auto Center Zona Norte", Cnpj = "11222333000343" };
        var deOutraRede = new Empresa { ClienteId = outra.Id, Nome = "Outra", Cnpj = "99888777000166" };
        _db.Empresas.AddRange(_centro, _norte, deOutraRede);
        await _db.SaveChangesAsync();
        Conta(_centro, 1, 10, 100m, SituacaoContaBling.EmAberto, "Oficina do Zé");     // vencida
        Conta(_centro, 2, 15, 250m, SituacaoContaBling.EmAberto, "Mecânica Silva");
        Conta(_norte, 3, 20, 80m, SituacaoContaBling.Pago, "Oficina do Zé");
        Conta(_norte, 4, 25, 60m, SituacaoContaBling.Cancelado, "Frota Beta");
        Conta(deOutraRede, 5, 15, 999m, SituacaoContaBling.EmAberto, "Não aparece");
        await _db.SaveChangesAsync();
    }

    private void Conta(Empresa empresa, long blingId, int dia, decimal valor, int situacao, string cliente)
    {
        _db.ContasReceber.Add(new ContaReceberSincronizada
        {
            EmpresaId = empresa.Id, BlingId = blingId, Vencimento = new DateTime(2026, 11, dia),
            Valor = valor, Situacao = situacao, ClienteNome = cliente
        });
    }

    private async Task<PaginaContasReceberDto> Listar(string? situacao = null, string? busca = null, string? ordem = null, int? empresaId = null)
    {
        var filtro = new FiltroContasReceber { Situacao = situacao, Busca = busca, Ordem = ordem, EmpresaId = empresaId };
        Resultado<PaginaContasReceberDto> resultado = await CriarServico().ListarAsync(_rede.Id, filtro, 1, CancellationToken.None);
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return resultado.Valor!;
    }

    [Fact]
    public async Task Lista_so_as_contas_da_rede_por_vencimento()
    {
        await CadastrarRedes();

        PaginaContasReceberDto pagina = await Listar();

        Assert.Equal(new long[] { 1, 2, 3, 4 }, pagina.Itens.Select(c => c.BlingId).ToArray());
        Assert.Equal(490m, pagina.ValorTotal);
        Assert.True(pagina.FluxoCaixaAtivo);
        ContaReceberDto vencida = pagina.Itens[0];
        Assert.True(vencida.Vencida);
        Assert.Equal("Em aberto", vencida.SituacaoTexto);
        Assert.Equal("Recebida", pagina.Itens[2].SituacaoTexto);
        Assert.Equal("Oficina do Zé", vencida.Cliente);
    }

    [Theory]
    [InlineData("Abertas", new long[] { 1, 2 })]
    [InlineData("Vencidas", new long[] { 1 })]
    [InlineData("Recebidas", new long[] { 3 })]
    [InlineData("Canceladas", new long[] { 4 })]
    public async Task Filtra_pela_situacao(string situacao, long[] esperadas)
    {
        await CadastrarRedes();

        PaginaContasReceberDto pagina = await Listar(situacao: situacao);

        Assert.Equal(esperadas, pagina.Itens.Select(c => c.BlingId).ToArray());
    }

    [Fact]
    public async Task Busca_por_cliente_empresa_e_ordena_por_cliente()
    {
        await CadastrarRedes();

        PaginaContasReceberDto porCliente = await Listar(busca: "oficina");
        PaginaContasReceberDto daNorte = await Listar(empresaId: _norte.Id, ordem: "-valor");
        PaginaContasReceberDto ordenada = await Listar(ordem: "cliente");

        Assert.Equal(new long[] { 1, 3 }, porCliente.Itens.Select(c => c.BlingId).ToArray());
        Assert.Equal(new long[] { 3, 4 }, daNorte.Itens.Select(c => c.BlingId).ToArray());
        Assert.Equal("Frota Beta", ordenada.Itens[0].Cliente);
    }

    [Fact]
    public async Task Situacao_e_ordem_invalidas_sao_recusadas()
    {
        await CadastrarRedes();
        var filtro = new FiltroContasReceber { Situacao = "Pagas" };
        var ordem = new FiltroContasReceber { Ordem = "fornecedor" };

        Resultado<PaginaContasReceberDto> situacao = await CriarServico().ListarAsync(_rede.Id, filtro, 1, CancellationToken.None);
        Resultado<PaginaContasReceberDto> ordenacao = await CriarServico().ListarAsync(_rede.Id, ordem, 1, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, situacao.Tipo);
        Assert.Equal(TipoResultado.Invalido, ordenacao.Tipo);
    }

    [Fact]
    public async Task Fluxo_de_caixa_liga_e_desliga_por_rede()
    {
        await CadastrarRedes();
        var servico = new FluxoCaixaService(new Repositorio<Cliente>(_db));

        Resultado<FluxoCaixaDto> antes = await servico.ObterAsync(_rede.Id, CancellationToken.None);
        Resultado<FluxoCaixaDto> desligado = await servico.AlterarAsync(_rede.Id, new FluxoCaixaDto { Ativo = false }, CancellationToken.None);
        Resultado<FluxoCaixaDto> inexistente = await servico.ObterAsync(999, CancellationToken.None);

        Assert.True(antes.Valor!.Ativo);
        Assert.False(desligado.Valor!.Ativo);
        Assert.False(_db.Clientes.Single(c => c.Id == _rede.Id).MostrarFluxoCaixa);
        Assert.Equal(TipoResultado.NaoEncontrado, inexistente.Tipo);
        PaginaContasReceberDto pagina = await Listar();
        Assert.False(pagina.FluxoCaixaAtivo);
    }
}
