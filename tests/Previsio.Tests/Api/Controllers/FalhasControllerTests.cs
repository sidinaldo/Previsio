using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Controllers;
using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Api.Controllers;

public class FalhasControllerTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();

    private FalhasController CriarController(ControllerContext usuario)
    {
        var controller = new FalhasController(ServicosDeTeste.Falhas(_db));
        controller.ControllerContext = usuario;
        return controller;
    }

    private async Task<int> CadastrarCliente(string nome)
    {
        var cliente = new Cliente();
        cliente.Nome = nome;
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return cliente.Id;
    }

    private FalhaProcessamento Falha(int clienteId, EtapaFalha etapa, DateTime ocorridaEm, int? notaId)
    {
        var falha = new FalhaProcessamento();
        falha.ClienteId = clienteId;
        falha.Etapa = etapa;
        falha.OcorridaEm = ocorridaEm;
        falha.NotaFiscalId = notaId;
        falha.Mensagem = "Erro " + etapa;
        falha.Metodo = "POST";
        falha.Endereco = "https://api.bling.com.br/Api/v3/contas/pagar";
        return falha;
    }

    private static Task<ActionResult<PaginaFalhasDto>> Listar(
        FalhasController controller, int clienteId, int? notaId, string? etapa, DateTime? de, DateTime? ate)
    {
        return controller.Listar(clienteId, null, notaId, etapa, de, ate, null, null, 1, CancellationToken.None);
    }

    [Fact]
    public async Task Lista_DaMaisRecenteParaAMaisAntiga_SoDoCliente()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        int beta = await CadastrarCliente("Rede Beta");
        _db.Falhas.Add(Falha(alfa, EtapaFalha.BuscaFornecedor, new DateTime(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc), 1));
        _db.Falhas.Add(Falha(alfa, EtapaFalha.LancamentoContaPagar, new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc), 1));
        _db.Falhas.Add(Falha(beta, EtapaFalha.LeituraEmail, new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc), null));
        await _db.SaveChangesAsync();

        ActionResult<PaginaFalhasDto> resposta = await Listar(CriarController(UsuarioDeTeste.Admin()), alfa, null, null, null, null);

        Assert.NotNull(resposta.Value);
        Assert.Equal(2, resposta.Value!.Total);
        Assert.Equal("LancamentoContaPagar", resposta.Value.Itens[0].Etapa);
        Assert.Equal("BuscaFornecedor", resposta.Value.Itens[1].Etapa);
    }

    [Fact]
    public async Task FiltraPorNotaEEtapa()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        _db.Falhas.Add(Falha(alfa, EtapaFalha.BuscaFornecedor, DateTime.UtcNow, 1));
        _db.Falhas.Add(Falha(alfa, EtapaFalha.LancamentoContaPagar, DateTime.UtcNow, 1));
        _db.Falhas.Add(Falha(alfa, EtapaFalha.LancamentoContaPagar, DateTime.UtcNow, 2));
        await _db.SaveChangesAsync();

        ActionResult<PaginaFalhasDto> resposta = await Listar(
            CriarController(UsuarioDeTeste.Admin()), alfa, 1, "LancamentoContaPagar", null, null);

        Assert.NotNull(resposta.Value);
        Assert.Equal(1, resposta.Value!.Total);
        Assert.Equal(1, resposta.Value.Itens[0].NotaFiscalId);
    }

    [Fact]
    public async Task FiltraPorPeriodo_NoHorarioDeBrasilia()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        // 10/11 às 01:00 UTC = 09/11 às 22:00 em Brasília
        _db.Falhas.Add(Falha(alfa, EtapaFalha.LeituraEmail, new DateTime(2026, 11, 10, 1, 0, 0, DateTimeKind.Utc), null));
        // 10/11 às 15:00 UTC = 10/11 às 12:00 em Brasília
        _db.Falhas.Add(Falha(alfa, EtapaFalha.LeituraEmail, new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc), null));
        await _db.SaveChangesAsync();

        DateTime dia10 = new DateTime(2026, 11, 10);
        ActionResult<PaginaFalhasDto> resposta = await Listar(
            CriarController(UsuarioDeTeste.Admin()), alfa, null, null, dia10, dia10);

        Assert.NotNull(resposta.Value);
        Assert.Equal(1, resposta.Value!.Total);
        Assert.Equal(15, resposta.Value.Itens[0].OcorridaEm.Hour);
    }

    [Fact]
    public async Task UsuarioDeOutroCliente_NaoAcessa()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        int beta = await CadastrarCliente("Rede Beta");

        ActionResult<PaginaFalhasDto> resposta = await Listar(
            CriarController(UsuarioDeTeste.DoCliente(beta)), alfa, null, null, null, null);

        Assert.IsType<ForbidResult>(resposta.Result);
    }

    [Fact]
    public async Task EtapaInvalida_RespondeBadRequest()
    {
        int alfa = await CadastrarCliente("Rede Alfa");

        ActionResult<PaginaFalhasDto> resposta = await Listar(
            CriarController(UsuarioDeTeste.Admin()), alfa, null, "Qualquer", null, null);

        Assert.IsType<BadRequestObjectResult>(resposta.Result);
    }
}
