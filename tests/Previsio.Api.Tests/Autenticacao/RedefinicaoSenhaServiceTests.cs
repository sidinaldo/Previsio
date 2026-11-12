using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Autenticacao;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Tests.Autenticacao;

public class RedefinicaoSenhaServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly EnvioEmailDeTeste _email = new EnvioEmailDeTeste();
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();

    private RedefinicaoSenhaService CriarServico()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Front:Url", "https://app.previsio.com.br/#" } })
            .Build();
        return new RedefinicaoSenhaService(
            _db, ServicosDeTeste.Autenticacao(_db), _email, configuracao, new AmbienteDeTeste(), _relogio,
            NullLogger<RedefinicaoSenhaService>.Instance);
    }

    private async Task<Usuario> CadastrarUsuario(bool ativo = true)
    {
        var usuario = new Usuario();
        usuario.Nome = "Ana Financeiro";
        usuario.Email = "ana@autocenter.com.br";
        usuario.Perfil = PerfilUsuario.Admin;
        usuario.Ativo = ativo;
        ServicosDeTeste.Autenticacao(_db).DefinirSenha(usuario, "senha-antiga-1");
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
        return usuario;
    }

    // O token só existe no link do e-mail
    private string TokenDoEmail()
    {
        string texto = _email.Enviados.Single().Texto;
        int inicio = texto.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        int fim = texto.IndexOf('\n', inicio);
        return Uri.UnescapeDataString(texto.Substring(inicio, fim - inicio));
    }

    [Fact]
    public async Task Solicitar_EnviaLinkParaOFrontEGuardaSoOHash()
    {
        Usuario usuario = await CadastrarUsuario();

        await CriarServico().SolicitarAsync(" ANA@autocenter.com.br ", CancellationToken.None);

        Assert.Single(_email.Enviados);
        Assert.Equal("ana@autocenter.com.br", _email.Enviados[0].Para);
        Assert.Contains("https://app.previsio.com.br/#/pages/redefinir-senha?token=", _email.Enviados[0].Html);
        string token = TokenDoEmail();
        Assert.NotEqual(token, usuario.RedefinicaoSenhaHash);
        Assert.Equal(RedefinicaoSenhaService.Hash(token), usuario.RedefinicaoSenhaHash);
        Assert.Equal(_relogio.Agora.UtcDateTime.AddHours(1), usuario.RedefinicaoSenhaExpiraEm);
    }

    [Fact]
    public async Task Solicitar_EmailDesconhecidoOuInativo_NaoEnviaNada()
    {
        await CadastrarUsuario(ativo: false);

        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);
        await CriarServico().SolicitarAsync("ninguem@autocenter.com.br", CancellationToken.None);

        Assert.Empty(_email.Enviados);
    }

    [Fact]
    public async Task Solicitar_ServidorDeEmailFora_NaoLancaErroParaQuemPediu()
    {
        await CadastrarUsuario();
        _email.Falhar = true;

        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);
    }

    [Fact]
    public async Task Redefinir_TrocaASenhaDerrubaSessoesEInvalidaOLink()
    {
        Usuario usuario = await CadastrarUsuario();
        string carimboAntes = usuario.CarimboSeguranca;
        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);
        string token = TokenDoEmail();

        Resultado resultado = await CriarServico().RedefinirAsync(token, "senha-nova-123", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        AutenticacaoService autenticacao = ServicosDeTeste.Autenticacao(_db);
        Assert.True(autenticacao.SenhaConfere(usuario, "senha-nova-123"));
        Assert.False(autenticacao.SenhaConfere(usuario, "senha-antiga-1"));
        Assert.NotEqual(carimboAntes, usuario.CarimboSeguranca);
        Assert.Null(usuario.RedefinicaoSenhaHash);

        // Uso único
        Resultado deNovo = await CriarServico().RedefinirAsync(token, "outra-senha-456", CancellationToken.None);
        Assert.Equal(TipoResultado.Invalido, deNovo.Tipo);
    }

    [Fact]
    public async Task Redefinir_DepoisDeUmaHora_LinkExpirado()
    {
        Usuario usuario = await CadastrarUsuario();
        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);
        string token = TokenDoEmail();

        _relogio.Agora = _relogio.Agora.AddMinutes(61);
        Resultado resultado = await CriarServico().RedefinirAsync(token, "senha-nova-123", CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Equal(RedefinicaoSenhaService.MensagemLinkInvalido, resultado.Mensagem);
        Assert.True(ServicosDeTeste.Autenticacao(_db).SenhaConfere(usuario, "senha-antiga-1"));
    }

    [Fact]
    public async Task Redefinir_PedidoNovoInvalidaOLinkAnterior()
    {
        await CadastrarUsuario();
        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);
        string primeiro = TokenDoEmail();
        _email.Enviados.Clear();
        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);

        Resultado resultado = await CriarServico().RedefinirAsync(primeiro, "senha-nova-123", CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Redefinir_SenhaCurta_InvalidaSemGastarOLink()
    {
        Usuario usuario = await CadastrarUsuario();
        await CriarServico().SolicitarAsync("ana@autocenter.com.br", CancellationToken.None);
        string token = TokenDoEmail();

        Resultado resultado = await CriarServico().RedefinirAsync(token, "curta", CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Contains("pelo menos 8", resultado.Mensagem);
        Assert.NotNull(usuario.RedefinicaoSenhaHash);
    }

    [Fact]
    public async Task Redefinir_TokenInventado_Invalido()
    {
        await CadastrarUsuario();

        Resultado resultado = await CriarServico().RedefinirAsync("qualquer-coisa", "senha-nova-123", CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }
}
