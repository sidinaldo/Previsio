using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Empresas;

namespace Previsio.Api.Tests.Servicos;

public class AcessoEmpresaServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();

    private async Task<int> CadastrarEmpresa(int clienteId)
    {
        var empresa = new Empresa();
        empresa.ClienteId = clienteId;
        empresa.Nome = "Matriz";
        empresa.Cnpj = "11222333000181";
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa.Id;
    }

    [Fact]
    public async Task UsuarioDoCliente_SoAcessaAsEmpresasDele()
    {
        int empresaId = await CadastrarEmpresa(1);
        AcessoEmpresaService acesso = ServicosDeTeste.Acesso(_db);

        Assert.True(await acesso.PodeAcessarAsync(UsuarioDeTeste.DoCliente(1).HttpContext.User, empresaId, CancellationToken.None));
        Assert.False(await acesso.PodeAcessarAsync(UsuarioDeTeste.DoCliente(2).HttpContext.User, empresaId, CancellationToken.None));
    }

    [Fact]
    public async Task Administrador_AcessaQualquerEmpresa()
    {
        int empresaId = await CadastrarEmpresa(1);

        bool pode = await ServicosDeTeste.Acesso(_db).PodeAcessarAsync(UsuarioDeTeste.Admin().HttpContext.User, empresaId, CancellationToken.None);

        Assert.True(pode);
    }
}
