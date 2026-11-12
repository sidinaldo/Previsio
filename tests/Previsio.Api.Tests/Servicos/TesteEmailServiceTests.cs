using System.Net.Sockets;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Xunit;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Tests.Servicos;

public class TesteEmailServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
    private readonly ConexaoImapDeTeste _conexao = new ConexaoImapDeTeste();

    private TesteEmailService CriarServico()
    {
        return new TesteEmailService(_conexao, new Repositorio<Cliente>(_db), _protetor);
    }

    private static TesteEmailDto DadosValidos()
    {
        var dados = new TesteEmailDto();
        dados.ImapHost = " imap.gmail.com ";
        dados.ImapPorta = 993;
        dados.ImapSsl = true;
        dados.ImapUsuario = "notas@alfa.com.br";
        dados.ImapSenha = "senha-de-app";
        return dados;
    }

    [Fact]
    public async Task Testar_QuandoEntra_RespondeConectado()
    {
        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(DadosValidos(), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor!.Conectou);
        Assert.Equal("imap.gmail.com", _conexao.HostRecebido);
        Assert.Equal("senha-de-app", _conexao.SenhaRecebida);
    }

    [Fact]
    public async Task Testar_SenhaRecusada_ExplicaASenhaDeApp()
    {
        _conexao.ErroParaLancar = new AuthenticationException("Invalid credentials");

        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(DadosValidos(), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor!.Conectou);
        Assert.Contains("senha de app", resultado.Valor.Mensagem);
    }

    [Fact]
    public async Task Testar_ServidorInexistente_PedeParaConferirServidorEPorta()
    {
        _conexao.ErroParaLancar = new SocketException((int)SocketError.HostNotFound);

        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(DadosValidos(), CancellationToken.None);

        Assert.False(resultado.Valor!.Conectou);
        Assert.Contains("imap.gmail.com:993", resultado.Valor.Mensagem);
    }

    [Fact]
    public async Task Testar_SemHostOuUsuario_EhInvalidoSemConectar()
    {
        TesteEmailDto dados = DadosValidos();
        dados.ImapHost = "  ";

        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(dados, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Equal(0, _conexao.Chamadas);
    }

    [Fact]
    public async Task Testar_SemSenhaESemCliente_EhInvalido()
    {
        TesteEmailDto dados = DadosValidos();
        dados.ImapSenha = "";

        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(dados, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Equal(0, _conexao.Chamadas);
    }

    [Fact]
    public async Task Testar_SemSenhaComCliente_UsaASenhaGravadaDaRede()
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        cliente.ImapHost = "imap.gmail.com";
        cliente.ImapUsuario = "notas@alfa.com.br";
        cliente.ImapSenhaProtegida = _protetor.Proteger("senha-gravada");
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();

        TesteEmailDto dados = DadosValidos();
        dados.ImapSenha = null;
        dados.ClienteId = cliente.Id;
        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(dados, CancellationToken.None);

        Assert.True(resultado.Valor!.Conectou);
        Assert.Equal("senha-gravada", _conexao.SenhaRecebida);
    }

    [Fact]
    public async Task Testar_SemSenhaComClienteInexistente_NaoEncontrado()
    {
        TesteEmailDto dados = DadosValidos();
        dados.ImapSenha = "";
        dados.ClienteId = 999;

        Resultado<ResultadoTesteEmailDto> resultado = await CriarServico().TestarAsync(dados, CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task Testar_RequisicaoCancelada_NaoViraMensagemDeErro()
    {
        _conexao.EsperarCancelamento = true;
        using var cancelada = new CancellationTokenSource();
        cancelada.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CriarServico().TestarAsync(DadosValidos(), cancelada.Token));
    }
}
