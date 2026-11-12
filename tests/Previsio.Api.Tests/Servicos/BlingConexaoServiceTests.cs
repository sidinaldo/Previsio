using System.Net;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Tests.Servicos;

public class BlingConexaoServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
    private readonly FilaDeJobsDeTeste _fila = new FilaDeJobsDeTeste();

    private BlingConexaoService CriarServico()
    {
        var respostas = new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.OK, "{}"));
        return ServicosDeTeste.ConexaoBling(_db, _protetor, BlingDeTeste.Criar(respostas), _fila);
    }

    private async Task<Empresa> CadastrarEmpresaConectada(int clienteId)
    {
        var empresa = new Empresa();
        empresa.ClienteId = clienteId;
        empresa.Nome = "Matriz";
        empresa.Cnpj = "11222333000181";
        empresa.BlingAccessTokenProtegido = _protetor.Proteger("access");
        empresa.BlingRefreshTokenProtegido = _protetor.Proteger("refresh");
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddHours(2);
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa;
    }

    [Fact]
    public async Task UrlDeConexao_UsaOAplicativoDaEmpresa()
    {
        Empresa empresa = await CadastrarEmpresaConectada(1);

        Resultado<string> resultado = await CriarServico().MontarUrlConexaoAsync(
            empresa.Id, UsuarioDeTeste.DoCliente(1).HttpContext.User, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Contains("client_id=id-teste", resultado.Valor);
        Assert.Contains("state=", resultado.Valor);
    }

    [Fact]
    public async Task UrlDeConexao_AplicativoProprioIncompleto_EhInvalido()
    {
        Empresa empresa = await CadastrarEmpresaConectada(1);
        empresa.BlingClientId = "app-da-filial";
        await _db.SaveChangesAsync();

        Resultado<string> resultado = await CriarServico().MontarUrlConexaoAsync(
            empresa.Id, UsuarioDeTeste.DoCliente(1).HttpContext.User, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task UrlDeConexao_EmpresaDeOutroCliente_NaoEncontrada()
    {
        Empresa empresa = await CadastrarEmpresaConectada(1);

        Resultado<string> resultado = await CriarServico().MontarUrlConexaoAsync(
            empresa.Id, UsuarioDeTeste.DoCliente(2).HttpContext.User, CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
    }

    [Fact]
    public async Task Desconectar_ApagaOsTokens()
    {
        Empresa empresa = await CadastrarEmpresaConectada(1);

        Resultado resultado = await CriarServico().DesconectarAsync(
            empresa.Id, UsuarioDeTeste.DoCliente(1).HttpContext.User, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.False(empresa.BlingConectado);
        Assert.Null(empresa.BlingRefreshTokenProtegido);
    }

    [Fact]
    public async Task ConcluirConexao_SemCodigo_Erro()
    {
        string retorno = await CriarServico().ConcluirConexaoAsync(null, "estado", CancellationToken.None);

        Assert.Equal(RetornoConexaoBling.Erro, retorno);
        Assert.Empty(_fila.Enfileirados);
    }

    [Fact]
    public async Task ConcluirConexao_EstadoInvalido_Expirado()
    {
        string retorno = await CriarServico().ConcluirConexaoAsync("codigo", "estado-adulterado", CancellationToken.None);

        Assert.Equal(RetornoConexaoBling.Expirado, retorno);
    }
}
