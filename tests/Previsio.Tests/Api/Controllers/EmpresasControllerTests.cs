using Previsio.Aplicacao.Abstracoes.Erp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Controllers;
using Previsio.Tests.Apoio;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Infra.Seguranca;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Api.Controllers;

public class EmpresasControllerTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private EmpresasController CriarController(int clienteId)
    {
        var controller = new EmpresasController(ServicosDeTeste.Empresas(_db, _protetor), ServicosDeTeste.Certificados(_db, _protetor), ServicosDeTeste.Acesso(_db));
        controller.ControllerContext = UsuarioDeTeste.DoCliente(clienteId);
        return controller;
    }

    private async Task<Empresa> CadastrarEmpresaConectada()
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();

        var empresa = new Empresa();
        empresa.ClienteId = cliente.Id;
        empresa.Nome = "Filial Centro";
        empresa.Cnpj = "98765432000110";
        empresa.ErpClientId = "app-antigo";
        empresa.ErpClientSecretProtegido = _protetor.Proteger("segredo-antigo");
        empresa.ErpAccessTokenProtegido = _protetor.Proteger("access");
        empresa.ErpRefreshTokenProtegido = _protetor.Proteger("refresh");
        empresa.ErpTokenExpiraEm = DateTime.UtcNow.AddHours(2);
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa;
    }

    [Fact]
    public async Task CadastrarAplicativo_GuardaOSecretCriptografadoENaoDevolveNaTela()
    {
        Empresa empresa = await CadastrarEmpresaConectada();

        var dados = new AlteracaoEmpresaDto();
        dados.ErpClientId = "app-novo";
        dados.ErpClientSecret = "segredo-novo";
        ActionResult<EmpresaDto> resposta = await CriarController(empresa.ClienteId).Alterar(empresa.Id, dados, CancellationToken.None);

        Assert.NotNull(resposta.Value);
        Assert.Equal("app-novo", resposta.Value!.ErpClientId);
        Assert.True(resposta.Value.ErpClientSecretCadastrado);
        Assert.NotEqual("segredo-novo", empresa.ErpClientSecretProtegido);
        Assert.Equal("segredo-novo", _protetor.Revelar(empresa.ErpClientSecretProtegido!));
    }

    [Fact]
    public async Task TrocarOClientId_DesconectaAEmpresa()
    {
        Empresa empresa = await CadastrarEmpresaConectada();

        var dados = new AlteracaoEmpresaDto();
        dados.ErpClientId = "app-novo";
        dados.ErpClientSecret = "segredo-novo";
        await CriarController(empresa.ClienteId).Alterar(empresa.Id, dados, CancellationToken.None);

        Assert.False(empresa.ErpConectado);
        Assert.Null(empresa.ErpAccessTokenProtegido);
    }

    [Fact]
    public async Task TrocarSoOSecret_MantemAConexao()
    {
        Empresa empresa = await CadastrarEmpresaConectada();

        var dados = new AlteracaoEmpresaDto();
        dados.ErpClientId = "app-antigo";
        dados.ErpClientSecret = "segredo-regenerado";
        await CriarController(empresa.ClienteId).Alterar(empresa.Id, dados, CancellationToken.None);

        Assert.True(empresa.ErpConectado);
        Assert.Equal("segredo-regenerado", _protetor.Revelar(empresa.ErpClientSecretProtegido!));
    }

    [Fact]
    public async Task ClientIdSemSecret_EhRecusado()
    {
        Empresa empresa = await CadastrarEmpresaConectada();
        empresa.ErpClientId = null;
        empresa.ErpClientSecretProtegido = null;
        await _db.SaveChangesAsync();

        var dados = new AlteracaoEmpresaDto();
        dados.ErpClientId = "app-novo";
        ActionResult<EmpresaDto> resposta = await CriarController(empresa.ClienteId).Alterar(empresa.Id, dados, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resposta.Result);
    }

    [Fact]
    public async Task ClientIdVazio_VoltaAoAplicativoGlobal()
    {
        Empresa empresa = await CadastrarEmpresaConectada();

        var dados = new AlteracaoEmpresaDto();
        dados.ErpClientId = "";
        await CriarController(empresa.ClienteId).Alterar(empresa.Id, dados, CancellationToken.None);

        Assert.Null(empresa.ErpClientId);
        Assert.Null(empresa.ErpClientSecretProtegido);
        Assert.False(empresa.ErpConectado);

        IErp bling = BlingDeTeste.Criar(new RespostasFixas(_ => new HttpResponseMessage()));
        var tokens = ServicosDeTeste.TokenBlingService(bling, _db, _protetor, NullLogger<TokenBlingService>.Instance);
        Assert.Equal("id-teste", (await tokens.CredenciaisAsync(empresa, CancellationToken.None)).ClientId);
    }

    [Fact]
    public async Task EmpresaComAplicativoProprio_UsaAsCredenciaisDela()
    {
        Empresa empresa = await CadastrarEmpresaConectada();

        IErp bling = BlingDeTeste.Criar(new RespostasFixas(_ => new HttpResponseMessage()));
        var tokens = ServicosDeTeste.TokenBlingService(bling, _db, _protetor, NullLogger<TokenBlingService>.Instance);
        CredenciaisErp credenciais = await tokens.CredenciaisAsync(empresa, CancellationToken.None);

        Assert.Equal("app-antigo", credenciais.ClientId);
        Assert.Equal("segredo-antigo", credenciais.ClientSecret);
    }
}
