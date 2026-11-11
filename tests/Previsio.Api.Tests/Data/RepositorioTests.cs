using Microsoft.EntityFrameworkCore;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Xunit;

namespace Previsio.Api.Tests.Data;

public class RepositorioTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();

    private Repositorio<Cliente> CriarRepositorio()
    {
        return new Repositorio<Cliente>(_db);
    }

    private static Cliente NovoCliente(string nome)
    {
        var cliente = new Cliente();
        cliente.Nome = nome;
        return cliente;
    }

    [Fact]
    public async Task Adicionar_SoGravaDepoisDeSalvar()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();

        repositorio.Adicionar(NovoCliente("Rede Alfa"));
        Assert.Equal(0, await _db.Clientes.CountAsync());

        await repositorio.SalvarAsync(CancellationToken.None);
        Assert.Equal(1, await _db.Clientes.CountAsync());
    }

    [Fact]
    public async Task ObterPorId_DevolveAEntidadeOuNulo()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();
        Cliente cliente = NovoCliente("Rede Alfa");
        repositorio.Adicionar(cliente);
        await repositorio.SalvarAsync(CancellationToken.None);

        Cliente? encontrado = await repositorio.ObterPorIdAsync(cliente.Id, CancellationToken.None);
        Cliente? inexistente = await repositorio.ObterPorIdAsync(cliente.Id + 100, CancellationToken.None);

        Assert.NotNull(encontrado);
        Assert.Equal("Rede Alfa", encontrado!.Nome);
        Assert.Null(inexistente);
    }

    [Fact]
    public async Task Existe_AplicaACondicao()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();
        repositorio.Adicionar(NovoCliente("Rede Alfa"));
        await repositorio.SalvarAsync(CancellationToken.None);

        Assert.True(await repositorio.ExisteAsync(c => c.Nome == "Rede Alfa", CancellationToken.None));
        Assert.False(await repositorio.ExisteAsync(c => c.Nome == "Rede Beta", CancellationToken.None));
    }

    [Fact]
    public async Task Consultar_RastreiaParaAlterar()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();
        repositorio.Adicionar(NovoCliente("Rede Alfa"));
        await repositorio.SalvarAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();

        Cliente cliente = await repositorio.Consultar().FirstAsync();
        cliente.Nome = "Rede Alfa Matriz";
        await repositorio.SalvarAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();

        Assert.Equal("Rede Alfa Matriz", (await _db.Clientes.FirstAsync()).Nome);
    }

    [Fact]
    public async Task ConsultarSomenteLeitura_NaoGravaAlteracoes()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();
        repositorio.Adicionar(NovoCliente("Rede Alfa"));
        await repositorio.SalvarAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();

        Cliente cliente = await repositorio.ConsultarSomenteLeitura().FirstAsync();
        cliente.Nome = "Alterado sem rastreamento";
        await repositorio.SalvarAsync(CancellationToken.None);
        _db.ChangeTracker.Clear();

        Assert.Equal("Rede Alfa", (await _db.Clientes.FirstAsync()).Nome);
    }

    [Fact]
    public async Task Remover_ApagaAoSalvar()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();
        Cliente cliente = NovoCliente("Rede Alfa");
        repositorio.Adicionar(cliente);
        await repositorio.SalvarAsync(CancellationToken.None);

        repositorio.Remover(cliente);
        await repositorio.SalvarAsync(CancellationToken.None);

        Assert.Equal(0, await _db.Clientes.CountAsync());
    }

    [Fact]
    public async Task DescartarAlteracoes_EsqueceOQueNaoFoiSalvo()
    {
        Repositorio<Cliente> repositorio = CriarRepositorio();
        repositorio.Adicionar(NovoCliente("Rede Alfa"));

        repositorio.DescartarAlteracoes();
        await repositorio.SalvarAsync(CancellationToken.None);

        Assert.Equal(0, await _db.Clientes.CountAsync());
    }
}
