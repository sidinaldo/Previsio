using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Controllers;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;

namespace Previsio.Api.Tests.Controllers;

public class ClientesControllerTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private ClientesController CriarController()
    {
        var controller = new ClientesController(ServicosDeTeste.Clientes(_db, _protetor), ServicosDeTeste.Empresas(_db, _protetor));
        controller.ControllerContext = UsuarioDeTeste.Admin();
        return controller;
    }

    private async Task<Cliente> CadastrarCliente()
    {
        var cliente = new Cliente();
        cliente.Nome = "Rede Alfa";
        cliente.ImapHost = "imap.gmail.com";
        cliente.ImapPorta = 993;
        cliente.ImapSsl = true;
        cliente.ImapUsuario = "notas@alfa.com.br";
        cliente.ImapSenhaProtegida = _protetor.Proteger("senha-antiga");
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return cliente;
    }

    [Fact]
    public async Task Alterar_ComSenhaVazia_MantemASenhaAtual()
    {
        Cliente cliente = await CadastrarCliente();
        string senhaGravada = cliente.ImapSenhaProtegida;

        var dados = new AlteracaoClienteDto();
        dados.Nome = "Rede Alfa Matriz";
        dados.ImapSenha = "";
        ActionResult<ClienteDto> resposta = await CriarController().Alterar(cliente.Id, dados, CancellationToken.None);

        Assert.NotNull(resposta.Value);
        Assert.Equal("Rede Alfa Matriz", resposta.Value!.Nome);
        Assert.Equal(senhaGravada, cliente.ImapSenhaProtegida);
        Assert.Equal("senha-antiga", _protetor.Revelar(cliente.ImapSenhaProtegida));
    }

    [Fact]
    public async Task Alterar_SemOCampoSenha_MantemASenhaAtual()
    {
        Cliente cliente = await CadastrarCliente();

        var dados = new AlteracaoClienteDto();
        dados.ImapUsuario = "outra@alfa.com.br";
        await CriarController().Alterar(cliente.Id, dados, CancellationToken.None);

        Assert.Equal("outra@alfa.com.br", cliente.ImapUsuario);
        Assert.Equal("senha-antiga", _protetor.Revelar(cliente.ImapSenhaProtegida));
    }

    [Fact]
    public async Task Alterar_ComSenhaPreenchida_GravaASenhaNovaProtegida()
    {
        Cliente cliente = await CadastrarCliente();

        var dados = new AlteracaoClienteDto();
        dados.ImapSenha = "senha-nova";
        await CriarController().Alterar(cliente.Id, dados, CancellationToken.None);

        Assert.NotEqual("senha-nova", cliente.ImapSenhaProtegida);
        Assert.Equal("senha-nova", _protetor.Revelar(cliente.ImapSenhaProtegida));
    }

    [Fact]
    public async Task Alterar_CamposNulos_NaoMudamNada()
    {
        Cliente cliente = await CadastrarCliente();

        var dados = new AlteracaoClienteDto();
        dados.Ativo = false;
        ActionResult<ClienteDto> resposta = await CriarController().Alterar(cliente.Id, dados, CancellationToken.None);

        Assert.False(cliente.Ativo);
        Assert.Equal("Rede Alfa", cliente.Nome);
        Assert.Equal("imap.gmail.com", cliente.ImapHost);
        Assert.Equal(993, cliente.ImapPorta);
        Assert.True(cliente.ImapSsl);
        Assert.Equal("notas@alfa.com.br", cliente.ImapUsuario);
        Assert.False(resposta.Value!.Ativo);
    }

    [Fact]
    public async Task Alterar_ClienteInexistente_Responde404()
    {
        ActionResult<ClienteDto> resposta = await CriarController().Alterar(999, new AlteracaoClienteDto(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resposta.Result);
    }

    [Fact]
    public async Task Alterar_TrocandoOUsuarioDoEmail_RecomecaALeituraDaCaixa()
    {
        Cliente cliente = await CadastrarCliente();
        cliente.ImapUidValidity = 7;
        cliente.ImapUltimoUid = 340;
        await _db.SaveChangesAsync();

        var dados = new AlteracaoClienteDto();
        dados.ImapUsuario = "notas2@alfa.com.br";
        await CriarController().Alterar(cliente.Id, dados, CancellationToken.None);

        Assert.Null(cliente.ImapUidValidity);
        Assert.Null(cliente.ImapUltimoUid);
    }

    [Fact]
    public async Task Alterar_SemTrocarACaixa_MantemOUltimoEmailLido()
    {
        Cliente cliente = await CadastrarCliente();
        cliente.ImapUidValidity = 7;
        cliente.ImapUltimoUid = 340;
        await _db.SaveChangesAsync();

        var dados = new AlteracaoClienteDto();
        dados.Nome = "Rede Alfa Matriz";
        dados.ImapUsuario = "NOTAS@alfa.com.br";
        await CriarController().Alterar(cliente.Id, dados, CancellationToken.None);

        Assert.Equal(7L, cliente.ImapUidValidity);
        Assert.Equal(340L, cliente.ImapUltimoUid);
    }
}
