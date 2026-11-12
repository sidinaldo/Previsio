using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Tests.Servicos;

public class UsuariosServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();

    private UsuariosService CriarServico()
    {
        return ServicosDeTeste.Usuarios(_db);
    }

    private async Task<int> CadastrarCliente()
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return cliente.Id;
    }

    private static NovoUsuarioDto Novo(string email, string perfil, int? clienteId)
    {
        var dados = new NovoUsuarioDto();
        dados.Nome = "Financeiro";
        dados.Email = email;
        dados.Senha = "senha-boa-123";
        dados.Perfil = perfil;
        dados.ClienteId = clienteId;
        return dados;
    }

    [Fact]
    public async Task Criar_UsuarioDeCliente_GravaComSenhaCriptografada()
    {
        int clienteId = await CadastrarCliente();

        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(
            Novo(" Financeiro@Alfa.com.br ", Perfis.Cliente, clienteId), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Usuario gravado = _db.Usuarios.Single();
        Assert.Equal("financeiro@alfa.com.br", gravado.Email);
        Assert.NotEqual("senha-boa-123", gravado.SenhaHash);
        Assert.Equal(clienteId, gravado.ClienteId);
    }

    [Fact]
    public async Task Criar_AdministradorComCliente_EhInvalido()
    {
        int clienteId = await CadastrarCliente();

        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(
            Novo("admin@softio.com.br", Perfis.Admin, clienteId), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Criar_ClienteInexistente_NaoEncontrado()
    {
        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(
            Novo("financeiro@alfa.com.br", Perfis.Cliente, 999), CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task Criar_EmailRepetido_EhConflito()
    {
        int clienteId = await CadastrarCliente();
        await CriarServico().CriarAsync(Novo("financeiro@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);

        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(
            Novo("FINANCEIRO@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflito, resultado.Tipo);
    }

    [Fact]
    public async Task Alterar_DesativarOProprioUsuario_EhInvalido()
    {
        int clienteId = await CadastrarCliente();
        Resultado<UsuarioDto> criado = await CriarServico().CriarAsync(
            Novo("financeiro@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);
        int usuarioId = criado.Valor!.Id;

        var dados = new AlteracaoUsuarioDto();
        dados.Ativo = false;
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(usuarioId, dados, usuarioId, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.True(_db.Usuarios.Single().Ativo);
    }

    [Fact]
    public async Task Alterar_DesativarOutroUsuario_DerrubaAsSessoes()
    {
        int clienteId = await CadastrarCliente();
        Resultado<UsuarioDto> criado = await CriarServico().CriarAsync(
            Novo("financeiro@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);
        string carimboAntes = _db.Usuarios.Single().CarimboSeguranca;

        var dados = new AlteracaoUsuarioDto();
        dados.Ativo = false;
        int outroUsuarioLogado = criado.Valor!.Id + 100;
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(criado.Valor.Id, dados, outroUsuarioLogado, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Usuario usuario = _db.Usuarios.Single();
        Assert.False(usuario.Ativo);
        Assert.NotEqual(carimboAntes, usuario.CarimboSeguranca);
    }

    [Fact]
    public async Task Alterar_UsuarioInexistente_NaoEncontrado()
    {
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(
            999, new AlteracaoUsuarioDto(), 1, CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }
}
