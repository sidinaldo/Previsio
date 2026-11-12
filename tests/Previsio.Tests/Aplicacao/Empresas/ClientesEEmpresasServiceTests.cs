using Microsoft.AspNetCore.DataProtection;
using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Infra.Seguranca;
using Previsio.Aplicacao.Empresas;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Aplicacao.Empresas;

public class ClientesEEmpresasServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private async Task<int> CadastrarCliente()
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return cliente.Id;
    }

    private static NovaEmpresaDto NovaEmpresa(string cnpj)
    {
        var dados = new NovaEmpresaDto();
        dados.Nome = "Matriz";
        dados.Cnpj = cnpj;
        return dados;
    }

    [Fact]
    public async Task CriarCliente_SemNome_EhInvalido()
    {
        var dados = new NovoClienteDto();
        dados.Nome = " ";

        Resultado<ClienteDto> resultado = await ServicosDeTeste.Clientes(_db, _protetor).CriarAsync(dados, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task CriarCliente_GuardaASenhaDoEmailCriptografada()
    {
        var dados = new NovoClienteDto();
        dados.Nome = "Rede Alfa";
        dados.ImapSenha = "senha-do-email";

        Resultado<ClienteDto> resultado = await ServicosDeTeste.Clientes(_db, _protetor).CriarAsync(dados, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Cliente gravado = _db.Clientes.Single();
        Assert.NotEqual("senha-do-email", gravado.ImapSenhaProtegida);
        Assert.Equal("senha-do-email", _protetor.Revelar(gravado.ImapSenhaProtegida));
    }

    [Fact]
    public async Task ObterCliente_Inexistente_NaoEncontrado()
    {
        Resultado<ClienteDto> resultado = await ServicosDeTeste.Clientes(_db, _protetor).ObterAsync(999, CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task CriarEmpresa_GuardaSoOsDigitosDoCnpj()
    {
        int clienteId = await CadastrarCliente();

        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor)
            .CriarAsync(clienteId, NovaEmpresa("11.222.333/0001-81"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("11222333000181", resultado.Valor!.Cnpj);
    }

    [Fact]
    public async Task CriarEmpresa_CnpjInvalido_EhInvalido()
    {
        int clienteId = await CadastrarCliente();

        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor)
            .CriarAsync(clienteId, NovaEmpresa("123"), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task CriarEmpresa_CnpjRepetidoNoCliente_EhConflito()
    {
        int clienteId = await CadastrarCliente();
        EmpresasService servico = ServicosDeTeste.Empresas(_db, _protetor);
        await servico.CriarAsync(clienteId, NovaEmpresa("11222333000181"), CancellationToken.None);

        Resultado<EmpresaDto> resultado = await servico.CriarAsync(clienteId, NovaEmpresa("11222333000181"), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflito, resultado.Tipo);
    }

    [Fact]
    public async Task CriarEmpresa_ClientIdSemSecret_EhInvalido()
    {
        int clienteId = await CadastrarCliente();
        NovaEmpresaDto dados = NovaEmpresa("11222333000181");
        dados.BlingClientId = "app-da-filial";

        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor)
            .CriarAsync(clienteId, dados, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Empty(_db.Empresas);
    }

    [Fact]
    public async Task CriarEmpresa_ClienteInexistente_NaoEncontrado()
    {
        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor)
            .CriarAsync(999, NovaEmpresa("11222333000181"), CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }
}
