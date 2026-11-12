using System.Security.Claims;
using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Usuarios;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Aplicacao.Usuarios;

public class UsuariosServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ClaimsPrincipal _admin = UsuarioDeTeste.PrincipalAdmin();

    private UsuariosService CriarServico()
    {
        return ServicosDeTeste.Usuarios(_db);
    }

    private async Task<int> CadastrarCliente(string nome = "Rede Alfa")
    {
        var cliente = new Cliente();
        cliente.Nome = nome;
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

        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(_admin,
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

        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(_admin,
            Novo("admin@softio.com.br", Perfis.Admin, clienteId), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Criar_ClienteInexistente_NaoEncontrado()
    {
        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(_admin,
            Novo("financeiro@alfa.com.br", Perfis.Cliente, 999), CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task Criar_EmailRepetido_EhConflito()
    {
        int clienteId = await CadastrarCliente();
        await CriarServico().CriarAsync(_admin, Novo("financeiro@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);

        Resultado<UsuarioDto> resultado = await CriarServico().CriarAsync(_admin,
            Novo("FINANCEIRO@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);

        Assert.Equal(TipoResultado.Conflito, resultado.Tipo);
    }

    [Fact]
    public async Task Alterar_DesativarOProprioUsuario_EhInvalido()
    {
        int clienteId = await CadastrarCliente();
        Resultado<UsuarioDto> criado = await CriarServico().CriarAsync(_admin,
            Novo("financeiro@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);
        int usuarioId = criado.Valor!.Id;

        var dados = new AlteracaoUsuarioDto();
        dados.Ativo = false;
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(UsuarioDeTeste.Principal(usuarioId, Perfis.Admin, null), usuarioId, dados, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.True(_db.Usuarios.Single().Ativo);
    }

    [Fact]
    public async Task Alterar_DesativarOutroUsuario_DerrubaAsSessoes()
    {
        int clienteId = await CadastrarCliente();
        Resultado<UsuarioDto> criado = await CriarServico().CriarAsync(_admin,
            Novo("financeiro@alfa.com.br", Perfis.Cliente, clienteId), CancellationToken.None);
        string carimboAntes = _db.Usuarios.Single().CarimboSeguranca;

        var dados = new AlteracaoUsuarioDto();
        dados.Ativo = false;
        int outroUsuarioLogado = criado.Valor!.Id + 100;
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(UsuarioDeTeste.Principal(outroUsuarioLogado, Perfis.Admin, null), criado.Valor.Id, dados, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Usuario usuario = _db.Usuarios.Single();
        Assert.False(usuario.Ativo);
        Assert.NotEqual(carimboAntes, usuario.CarimboSeguranca);
    }

    [Fact]
    public async Task Alterar_UsuarioInexistente_NaoEncontrado()
    {
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(
            _admin, 999, new AlteracaoUsuarioDto(), CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    // ---------- gestor da rede ----------

    private async Task<Usuario> CadastrarUsuario(int clienteId, string email, PerfilUsuario perfil)
    {
        var usuario = new Usuario();
        usuario.Nome = email;
        usuario.Email = email;
        usuario.Perfil = perfil;
        usuario.ClienteId = clienteId;
        usuario.SenhaHash = "x";
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario;
    }

    [Fact]
    public async Task Gestor_Lista_SoOsUsuariosDaPropriaRede()
    {
        int alfa = await CadastrarCliente();
        int beta = await CadastrarCliente("Rede Beta");
        Usuario gestor = await CadastrarUsuario(alfa, "gestor@alfa.com.br", PerfilUsuario.Gestor);
        await CadastrarUsuario(alfa, "compras@alfa.com.br", PerfilUsuario.Cliente);
        await CadastrarUsuario(beta, "financeiro@beta.com.br", PerfilUsuario.Cliente);

        // Mesmo pedindo outra rede, vem só a dele
        List<UsuarioDto> lista = await CriarServico().ListarAsync(
            UsuarioDeTeste.Principal(gestor.Id, Perfis.Gestor, alfa), beta, CancellationToken.None);

        Assert.Equal(new[] { "compras@alfa.com.br", "gestor@alfa.com.br" }, lista.Select(u => u.Email).OrderBy(e => e).ToArray());
    }

    [Fact]
    public async Task Gestor_Cria_SempreNaPropriaRede_EPodeCriarOutroGestor()
    {
        int alfa = await CadastrarCliente();
        int beta = await CadastrarCliente("Rede Beta");
        Usuario gestor = await CadastrarUsuario(alfa, "gestor@alfa.com.br", PerfilUsuario.Gestor);
        ClaimsPrincipal ator = UsuarioDeTeste.Principal(gestor.Id, Perfis.Gestor, alfa);

        Resultado<UsuarioDto> criado = await CriarServico().CriarAsync(ator, Novo("socio@alfa.com.br", Perfis.Gestor, beta), CancellationToken.None);
        Resultado<UsuarioDto> admin = await CriarServico().CriarAsync(ator, Novo("eu@alfa.com.br", Perfis.Admin, null), CancellationToken.None);

        Assert.True(criado.Sucesso);
        Assert.Equal(alfa, criado.Valor!.ClienteId);
        Assert.Equal(Perfis.Gestor, criado.Valor.Perfil);
        Assert.Equal(TipoResultado.Invalido, admin.Tipo);
    }

    [Fact]
    public async Task Gestor_NaoAlteraUsuarioDeOutraRede()
    {
        int alfa = await CadastrarCliente();
        int beta = await CadastrarCliente("Rede Beta");
        Usuario gestor = await CadastrarUsuario(alfa, "gestor@alfa.com.br", PerfilUsuario.Gestor);
        Usuario deBeta = await CadastrarUsuario(beta, "financeiro@beta.com.br", PerfilUsuario.Cliente);

        var dados = new AlteracaoUsuarioDto();
        dados.Ativo = false;
        Resultado<UsuarioDto> resultado = await CriarServico().AlterarAsync(
            UsuarioDeTeste.Principal(gestor.Id, Perfis.Gestor, alfa), deBeta.Id, dados, CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
        Assert.True(_db.Usuarios.Single(u => u.Id == deBeta.Id).Ativo);
    }

    [Fact]
    public async Task Gestor_PromoveOutro_MasNaoMudaOProprioPerfil()
    {
        int alfa = await CadastrarCliente();
        Usuario gestor = await CadastrarUsuario(alfa, "gestor@alfa.com.br", PerfilUsuario.Gestor);
        Usuario compras = await CadastrarUsuario(alfa, "compras@alfa.com.br", PerfilUsuario.Cliente);
        string carimboAntes = compras.CarimboSeguranca;
        ClaimsPrincipal ator = UsuarioDeTeste.Principal(gestor.Id, Perfis.Gestor, alfa);

        var promover = new AlteracaoUsuarioDto();
        promover.Perfil = Perfis.Gestor;
        Resultado<UsuarioDto> promovido = await CriarServico().AlterarAsync(ator, compras.Id, promover, CancellationToken.None);

        var rebaixar = new AlteracaoUsuarioDto();
        rebaixar.Perfil = Perfis.Cliente;
        Resultado<UsuarioDto> proprio = await CriarServico().AlterarAsync(ator, gestor.Id, rebaixar, CancellationToken.None);

        var virarAdmin = new AlteracaoUsuarioDto();
        virarAdmin.Perfil = Perfis.Admin;
        Resultado<UsuarioDto> admin = await CriarServico().AlterarAsync(ator, compras.Id, virarAdmin, CancellationToken.None);

        Assert.True(promovido.Sucesso);
        Assert.Equal(PerfilUsuario.Gestor, _db.Usuarios.Single(u => u.Id == compras.Id).Perfil);
        // O perfil vai no token: quem foi promovido entra de novo
        Assert.NotEqual(carimboAntes, _db.Usuarios.Single(u => u.Id == compras.Id).CarimboSeguranca);
        Assert.Equal(TipoResultado.Invalido, proprio.Tipo);
        Assert.Equal(TipoResultado.Invalido, admin.Tipo);
    }

    [Fact]
    public async Task Alterar_Email_ConfereSeJaEstaEmUso()
    {
        int alfa = await CadastrarCliente();
        Usuario compras = await CadastrarUsuario(alfa, "compras@alfa.com.br", PerfilUsuario.Cliente);
        await CadastrarUsuario(alfa, "financeiro@alfa.com.br", PerfilUsuario.Cliente);

        var repetido = new AlteracaoUsuarioDto();
        repetido.Email = "FINANCEIRO@alfa.com.br";
        Resultado<UsuarioDto> conflito = await CriarServico().AlterarAsync(_admin, compras.Id, repetido, CancellationToken.None);

        var novo = new AlteracaoUsuarioDto();
        novo.Email = " Compras2@Alfa.com.br ";
        Resultado<UsuarioDto> trocado = await CriarServico().AlterarAsync(_admin, compras.Id, novo, CancellationToken.None);

        Assert.Equal(TipoResultado.Conflito, conflito.Tipo);
        Assert.True(trocado.Sucesso);
        Assert.Equal("compras2@alfa.com.br", _db.Usuarios.Single(u => u.Id == compras.Id).Email);
    }
}
