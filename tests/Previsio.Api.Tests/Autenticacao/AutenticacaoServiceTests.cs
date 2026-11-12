using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Infra.Seguranca;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Tests.Autenticacao;

public class AutenticacaoServiceTests
{
    private const string Email = "financeiro@alfa.com.br";
    private const string Senha = "senha-boa-123";

    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly AutenticacaoService _servico;

    public AutenticacaoServiceTests()
    {
        _servico = ServicosDeTeste.AutenticacaoService(_db, new HashSenhaIdentity(), NullLogger<AutenticacaoService>.Instance);
    }

    private async Task CadastrarUsuarioDeCliente(bool clienteAtivo)
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        cliente.Ativo = clienteAtivo;
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();

        var usuario = new Usuario();
        usuario.Nome = "Financeiro";
        usuario.Email = Email;
        usuario.Perfil = PerfilUsuario.Cliente;
        usuario.ClienteId = cliente.Id;
        _servico.DefinirSenha(usuario, Senha);
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_DeUsuarioDeClienteInativo_ERecusado()
    {
        await CadastrarUsuarioDeCliente(false);

        Usuario? usuario = await _servico.AutenticarAsync(Email, Senha, CancellationToken.None);

        Assert.Null(usuario);
    }

    [Fact]
    public async Task Login_DeUsuarioDeClienteAtivo_EAceito()
    {
        await CadastrarUsuarioDeCliente(true);

        Usuario? usuario = await _servico.AutenticarAsync(Email, Senha, CancellationToken.None);

        Assert.NotNull(usuario);
        Assert.Equal(Email, usuario!.Email);
    }

    [Fact]
    public async Task TrocarSenha_SenhaAtualErrada_EhInvalido()
    {
        await CadastrarUsuarioDeCliente(true);
        Usuario usuario = _db.Usuarios.Single();

        Previsio.Aplicacao.Comum.Resultado resultado = await _servico.TrocarSenhaAsync(
            usuario, "senha-errada", "nova-senha-456", CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Senha atual incorreta.", resultado.Mensagem);
    }

    [Fact]
    public async Task TrocarSenha_GravaANovaEDerrubaAsOutrasSessoes()
    {
        await CadastrarUsuarioDeCliente(true);
        Usuario usuario = _db.Usuarios.Single();
        string carimboAntes = usuario.CarimboSeguranca;

        Previsio.Aplicacao.Comum.Resultado resultado = await _servico.TrocarSenhaAsync(
            usuario, Senha, "nova-senha-456", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(_servico.SenhaConfere(usuario, "nova-senha-456"));
        Assert.NotEqual(carimboAntes, usuario.CarimboSeguranca);
    }
}
