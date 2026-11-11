using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Controllers;
using Previsio.Api.Dtos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;

namespace Previsio.Api.Tests.Controllers;

public class NotasControllerTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();

    private NotasController CriarController(ControllerContext usuario)
    {
        var controller = new NotasController(ServicosDeTeste.Notas(_db, new FilaDeJobsDeTeste()));
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

    private async Task<int> CadastrarEmpresa(int clienteId, string nome, string cnpj)
    {
        var empresa = new Empresa();
        empresa.ClienteId = clienteId;
        empresa.Nome = nome;
        empresa.Cnpj = cnpj;
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return empresa.Id;
    }

    private NotaFiscal Nota(int clienteId, int? empresaId, string numero, StatusNota status, DateTime recebidaEm)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = clienteId;
        nota.EmpresaId = empresaId;
        nota.Numero = numero;
        nota.ChaveAcesso = "3526111122233300018155001000000" + numero.PadLeft(6, '0') + "0000001";
        nota.NomeFornecedor = "Fornecedor " + numero;
        nota.Status = status;
        nota.RecebidaEm = recebidaEm;
        nota.ValorTotal = 100m;
        return nota;
    }

    private static Task<ActionResult<PaginaNotasDto>> Listar(
        NotasController controller, int clienteId, int? empresaId, string? situacao, DateTime? de, DateTime? ate, int pagina)
    {
        return controller.Listar(clienteId, empresaId, situacao, de, ate, pagina, CancellationToken.None);
    }

    [Fact]
    public async Task Lista_LancadasEPendentes_DaMaisRecente_SoDoCliente_ComParcelas()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        int beta = await CadastrarCliente("Rede Beta");
        int matriz = await CadastrarEmpresa(alfa, "Matriz", "11222333000181");

        NotaFiscal lancada = Nota(alfa, matriz, "1001", StatusNota.Lancada, new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc));
        lancada.LancadaEm = new DateTime(2026, 11, 10, 15, 2, 0, DateTimeKind.Utc);
        var segunda = new ParcelaNota();
        segunda.NumeroParcela = "002";
        segunda.Vencimento = new DateTime(2026, 12, 17);
        segunda.Valor = 50m;
        segunda.BlingContaPagarId = 222;
        var primeira = new ParcelaNota();
        primeira.NumeroParcela = "001";
        primeira.Vencimento = new DateTime(2026, 11, 17);
        primeira.Valor = 50m;
        primeira.BlingContaPagarId = 111;
        lancada.Parcelas.Add(segunda);
        lancada.Parcelas.Add(primeira);

        _db.NotasFiscais.Add(Nota(alfa, matriz, "1000", StatusNota.Erro, new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc)));
        _db.NotasFiscais.Add(lancada);
        _db.NotasFiscais.Add(Nota(beta, null, "2000", StatusNota.Lancada, new DateTime(2026, 11, 10, 16, 0, 0, DateTimeKind.Utc)));
        await _db.SaveChangesAsync();

        ActionResult<PaginaNotasDto> resposta = await Listar(CriarController(UsuarioDeTeste.Admin()), alfa, null, null, null, null, 1);

        Assert.NotNull(resposta.Value);
        Assert.Equal(2, resposta.Value!.Total);
        NotaRecebidaDto maisRecente = resposta.Value.Itens[0];
        Assert.Equal("1001", maisRecente.Numero);
        Assert.Equal("Lancada", maisRecente.Status);
        Assert.Equal("Matriz", maisRecente.EmpresaNome);
        Assert.Equal(lancada.LancadaEm, maisRecente.LancadaEm);
        Assert.Equal(new DateTime(2026, 11, 17), maisRecente.PrimeiroVencimento);
        Assert.Equal(new[] { "001", "002" }, maisRecente.Parcelas.Select(p => p.NumeroParcela).ToArray());
        Assert.Equal(111, maisRecente.Parcelas[0].BlingContaPagarId);
        Assert.Equal("1000", resposta.Value.Itens[1].Numero);
    }

    [Fact]
    public async Task FiltraPorSituacao_LancadaOuPendentes()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        _db.NotasFiscais.Add(Nota(alfa, null, "1", StatusNota.Lancada, DateTime.UtcNow));
        _db.NotasFiscais.Add(Nota(alfa, null, "2", StatusNota.Erro, DateTime.UtcNow));
        _db.NotasFiscais.Add(Nota(alfa, null, "3", StatusNota.EmpresaNaoEncontrada, DateTime.UtcNow));
        await _db.SaveChangesAsync();
        NotasController controller = CriarController(UsuarioDeTeste.Admin());

        ActionResult<PaginaNotasDto> lancadas = await Listar(controller, alfa, null, "lancada", null, null, 1);
        ActionResult<PaginaNotasDto> pendentes = await Listar(controller, alfa, null, "Pendentes", null, null, 1);

        Assert.Equal(new[] { "1" }, lancadas.Value!.Itens.Select(n => n.Numero).ToArray());
        Assert.Equal(2, pendentes.Value!.Total);
        Assert.DoesNotContain(pendentes.Value.Itens, n => n.Status == "Lancada");
    }

    [Fact]
    public async Task SituacaoInvalida_Responde400()
    {
        int alfa = await CadastrarCliente("Rede Alfa");

        ActionResult<PaginaNotasDto> resposta = await Listar(CriarController(UsuarioDeTeste.Admin()), alfa, null, "Paga", null, null, 1);

        Assert.IsType<BadRequestObjectResult>(resposta.Result);
    }

    [Fact]
    public async Task FiltraPorEmpresaEPorDiaDeChegada_NoHorarioDeBrasilia()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        int matriz = await CadastrarEmpresa(alfa, "Matriz", "11222333000181");
        int filial = await CadastrarEmpresa(alfa, "Filial", "11222333000262");
        // 10/11 às 23h30 em Brasília = 11/11 às 02h30 UTC: conta como dia 10
        _db.NotasFiscais.Add(Nota(alfa, matriz, "10", StatusNota.Lancada, new DateTime(2026, 11, 11, 2, 30, 0, DateTimeKind.Utc)));
        _db.NotasFiscais.Add(Nota(alfa, matriz, "11", StatusNota.Lancada, new DateTime(2026, 11, 11, 3, 30, 0, DateTimeKind.Utc)));
        _db.NotasFiscais.Add(Nota(alfa, filial, "12", StatusNota.Lancada, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc)));
        await _db.SaveChangesAsync();

        ActionResult<PaginaNotasDto> resposta = await Listar(CriarController(UsuarioDeTeste.Admin()), alfa, matriz, null,
            new DateTime(2026, 11, 10), new DateTime(2026, 11, 10), 1);

        Assert.Equal(new[] { "10" }, resposta.Value!.Itens.Select(n => n.Numero).ToArray());
    }

    [Fact]
    public async Task Pagina_De50EmQuantidade()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        DateTime inicio = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 1; i <= 55; i++)
        {
            _db.NotasFiscais.Add(Nota(alfa, null, i.ToString(), StatusNota.Lancada, inicio.AddMinutes(i)));
        }
        await _db.SaveChangesAsync();

        ActionResult<PaginaNotasDto> segunda = await Listar(CriarController(UsuarioDeTeste.Admin()), alfa, null, null, null, null, 2);

        Assert.Equal(55, segunda.Value!.Total);
        Assert.Equal(NotasController.TamanhoPagina, segunda.Value.TamanhoPagina);
        Assert.Equal(5, segunda.Value.Itens.Count);
        Assert.Equal("5", segunda.Value.Itens[0].Numero);
    }

    [Fact]
    public async Task UsuarioDeOutroCliente_NaoVeAsNotas()
    {
        int alfa = await CadastrarCliente("Rede Alfa");
        int beta = await CadastrarCliente("Rede Beta");
        _db.NotasFiscais.Add(Nota(alfa, null, "1", StatusNota.Lancada, DateTime.UtcNow));
        await _db.SaveChangesAsync();

        ActionResult<PaginaNotasDto> resposta = await Listar(CriarController(UsuarioDeTeste.DoCliente(beta)), alfa, null, null, null, null, 1);

        Assert.IsType<ForbidResult>(resposta.Result);
    }
}
