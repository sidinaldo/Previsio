using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Tests.Servicos;

/// <summary>
/// E-mail e aplicativo do Bling: o da empresa, quando configurado; senão, o da rede.
/// </summary>
public class RedeEEmpresasConfiguracaoTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private async Task<Cliente> CadastrarRede(string? clientId, string? secret)
    {
        var rede = new Cliente();
        rede.Nome = "Rede Alfa";
        rede.BlingClientId = clientId;
        if (secret != null)
        {
            rede.BlingClientSecretProtegido = _protetor.Proteger(secret);
        }
        _db.Clientes.Add(rede);
        await _db.SaveChangesAsync();
        return rede;
    }

    private async Task<Empresa> CadastrarEmpresaConectada(Cliente rede, string cnpj, string? clientIdProprio)
    {
        var empresa = new Empresa();
        empresa.ClienteId = rede.Id;
        empresa.Nome = "Empresa " + cnpj;
        empresa.Cnpj = cnpj;
        if (clientIdProprio != null)
        {
            empresa.BlingClientId = clientIdProprio;
            empresa.BlingClientSecretProtegido = _protetor.Proteger("segredo-proprio");
        }
        empresa.BlingAccessTokenProtegido = _protetor.Proteger("access");
        empresa.BlingRefreshTokenProtegido = _protetor.Proteger("refresh");
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddHours(2);
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa;
    }

    private TokenBlingService Tokens()
    {
        BlingClient bling = BlingDeTeste.Criar(new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.OK, "{}")));
        return new TokenBlingService(bling, _db, _protetor, NullLogger<TokenBlingService>.Instance);
    }

    // ---------- aplicativo do Bling: empresa -> rede -> global ----------

    [Fact]
    public async Task Credenciais_EmpresaComAplicativoProprio_UsaODela()
    {
        Cliente rede = await CadastrarRede("app-da-rede", "segredo-da-rede");
        Empresa empresa = await CadastrarEmpresaConectada(rede, "11222333000181", "app-da-filial");

        CredenciaisBling credenciais = await Tokens().CredenciaisAsync(empresa, CancellationToken.None);

        Assert.Equal("app-da-filial", credenciais.ClientId);
        Assert.Equal("segredo-proprio", credenciais.ClientSecret);
    }

    [Fact]
    public async Task Credenciais_EmpresaSemAplicativo_UsaODaRede()
    {
        Cliente rede = await CadastrarRede("app-da-rede", "segredo-da-rede");
        Empresa empresa = await CadastrarEmpresaConectada(rede, "11222333000181", null);
        empresa.Cliente = null;

        CredenciaisBling credenciais = await Tokens().CredenciaisAsync(empresa, CancellationToken.None);

        Assert.Equal("app-da-rede", credenciais.ClientId);
        Assert.Equal("segredo-da-rede", credenciais.ClientSecret);
    }

    [Fact]
    public async Task Credenciais_NemEmpresaNemRede_UsaOGlobal()
    {
        Cliente rede = await CadastrarRede(null, null);
        Empresa empresa = await CadastrarEmpresaConectada(rede, "11222333000181", null);

        CredenciaisBling credenciais = await Tokens().CredenciaisAsync(empresa, CancellationToken.None);

        Assert.Equal("id-teste", credenciais.ClientId);
    }

    // ---------- aplicativo do Bling da rede ----------

    [Fact]
    public async Task Rede_TrocarOAplicativo_DesconectaSoQuemUsaODaRede()
    {
        Cliente rede = await CadastrarRede("app-antigo", "segredo-antigo");
        Empresa usaRede = await CadastrarEmpresaConectada(rede, "11222333000181", null);
        Empresa usaProprio = await CadastrarEmpresaConectada(rede, "11222333000262", "app-da-filial");

        var dados = new AlteracaoClienteDto();
        dados.BlingClientId = "app-novo";
        dados.BlingClientSecret = "segredo-novo";
        Resultado<ClienteDto> resultado = await ServicosDeTeste.Clientes(_db, _protetor).AlterarAsync(rede.Id, dados, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("app-novo", resultado.Valor!.BlingClientId);
        Assert.True(resultado.Valor.BlingClientSecretCadastrado);
        Assert.False(usaRede.BlingConectado);
        Assert.True(usaProprio.BlingConectado);
    }

    [Fact]
    public async Task Rede_TrocarSoOSecret_MantemAsConexoes()
    {
        Cliente rede = await CadastrarRede("app-da-rede", "segredo-antigo");
        Empresa usaRede = await CadastrarEmpresaConectada(rede, "11222333000181", null);

        var dados = new AlteracaoClienteDto();
        dados.BlingClientSecret = "segredo-regenerado";
        await ServicosDeTeste.Clientes(_db, _protetor).AlterarAsync(rede.Id, dados, CancellationToken.None);

        Assert.True(usaRede.BlingConectado);
        Assert.Equal("segredo-regenerado", _protetor.Revelar(rede.BlingClientSecretProtegido!));
    }

    [Fact]
    public async Task Rede_ClientIdSemSecret_EhInvalido()
    {
        Cliente rede = await CadastrarRede(null, null);

        var dados = new AlteracaoClienteDto();
        dados.BlingClientId = "app-da-rede";
        Resultado<ClienteDto> resultado = await ServicosDeTeste.Clientes(_db, _protetor).AlterarAsync(rede.Id, dados, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Empresa_MostraDeOndeVemOAplicativo()
    {
        Cliente rede = await CadastrarRede("app-da-rede", "segredo-da-rede");
        await CadastrarEmpresaConectada(rede, "11222333000181", null);
        await CadastrarEmpresaConectada(rede, "11222333000262", "app-da-filial");
        _db.ChangeTracker.Clear();

        List<EmpresaDto> empresas = await ServicosDeTeste.Empresas(_db, _protetor).ListarDoClienteAsync(rede.Id, CancellationToken.None);

        Assert.Equal(OrigemAplicativoBling.Rede, empresas.Single(e => e.Cnpj == "11222333000181").OrigemAplicativoBling);
        Assert.Equal(OrigemAplicativoBling.Empresa, empresas.Single(e => e.Cnpj == "11222333000262").OrigemAplicativoBling);
    }

    // ---------- e-mail próprio da empresa ----------

    [Fact]
    public async Task Empresa_CadastrarEmailProprio_GuardaASenhaCriptografada()
    {
        Cliente rede = await CadastrarRede(null, null);
        Empresa empresa = await CadastrarEmpresaConectada(rede, "11222333000181", null);

        var dados = new AlteracaoEmpresaDto();
        dados.ImapHost = "imap.gmail.com";
        dados.ImapPorta = 993;
        dados.ImapSsl = true;
        dados.ImapUsuario = "filial@rede.com.br";
        dados.ImapSenha = "senha-de-app";
        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor)
            .AlterarAsync(empresa.Id, dados, UsuarioDeTeste.Admin().HttpContext.User, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor!.EmailProprio);
        Assert.True(resultado.Valor.ImapSenhaCadastrada);
        Assert.Equal("senha-de-app", _protetor.Revelar(empresa.ImapSenhaProtegida));
    }

    [Fact]
    public async Task Empresa_TrocarOUsuarioDoEmail_RecomecaALeitura()
    {
        Cliente rede = await CadastrarRede(null, null);
        Empresa empresa = await CadastrarEmpresaConectada(rede, "11222333000181", null);
        empresa.ImapHost = "imap.gmail.com";
        empresa.ImapUsuario = "filial@rede.com.br";
        empresa.ImapSenhaProtegida = _protetor.Proteger("senha");
        empresa.ImapUidValidity = 7;
        empresa.ImapUltimoUid = 340;
        await _db.SaveChangesAsync();

        var dados = new AlteracaoEmpresaDto();
        dados.ImapUsuario = "filial2@rede.com.br";
        await ServicosDeTeste.Empresas(_db, _protetor)
            .AlterarAsync(empresa.Id, dados, UsuarioDeTeste.Admin().HttpContext.User, CancellationToken.None);

        Assert.Null(empresa.ImapUidValidity);
        Assert.Null(empresa.ImapUltimoUid);
    }

    [Fact]
    public async Task Empresa_ServidorVazio_RemoveOEmailProprio()
    {
        Cliente rede = await CadastrarRede(null, null);
        Empresa empresa = await CadastrarEmpresaConectada(rede, "11222333000181", null);
        empresa.ImapHost = "imap.gmail.com";
        empresa.ImapUsuario = "filial@rede.com.br";
        empresa.ImapSenhaProtegida = _protetor.Proteger("senha");
        await _db.SaveChangesAsync();

        var dados = new AlteracaoEmpresaDto();
        dados.ImapHost = "";
        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor)
            .AlterarAsync(empresa.Id, dados, UsuarioDeTeste.Admin().HttpContext.User, CancellationToken.None);

        Assert.False(resultado.Valor!.EmailProprio);
        Assert.Equal("", empresa.ImapUsuario);
        Assert.Equal("", empresa.ImapSenhaProtegida);
    }

    [Fact]
    public void FalhaDeEmailDaEmpresa_ApontaAEmpresa()
    {
        var empresa = new Empresa();
        empresa.ImapHost = "imap.gmail.com";
        empresa.ImapUsuario = "filial@rede.com.br";

        FalhaProcessamento falha = FabricaFalha.DoEmail(1, 10, empresa, "Falha ao conectar", null);

        Assert.Equal(10, falha.EmpresaId);
        Assert.Contains("filial@rede.com.br", falha.Endereco);
    }

    [Fact]
    public async Task Empresa_CadastroComEmailProprio()
    {
        Cliente rede = await CadastrarRede(null, null);

        var dados = new NovaEmpresaDto();
        dados.Nome = "Filial Norte";
        dados.Cnpj = "11222333000262";
        dados.ImapHost = "imap.gmail.com";
        dados.ImapUsuario = "norte@rede.com.br";
        dados.ImapSenha = "senha-de-app";
        Resultado<EmpresaDto> resultado = await ServicosDeTeste.Empresas(_db, _protetor).CriarAsync(rede.Id, dados, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor!.EmailProprio);
        Assert.Equal(993, resultado.Valor.ImapPorta);
        Assert.True(resultado.Valor.ImapSsl);
        Assert.Equal(OrigemAplicativoBling.Padrao, resultado.Valor.OrigemAplicativoBling);
    }
}
