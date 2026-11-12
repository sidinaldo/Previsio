using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Tests.Servicos;

public class NotasServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly FilaDeJobsDeTeste _fila = new FilaDeJobsDeTeste();

    private async Task<NotaFiscal> CadastrarNota(StatusNota status, bool comParcela)
    {
        var empresa = new Empresa();
        empresa.ClienteId = 1;
        empresa.Nome = "Matriz";
        empresa.Cnpj = "11222333000181";
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();

        var nota = new NotaFiscal();
        nota.ClienteId = 1;
        nota.EmpresaId = empresa.Id;
        nota.Numero = "123";
        nota.ChaveAcesso = "35261111222333000181550010000001231000001230";
        nota.Status = status;
        nota.Tentativas = 3;
        nota.MensagemErro = "Erro anterior";
        if (comParcela)
        {
            var parcela = new ParcelaNota();
            parcela.NumeroParcela = "001";
            parcela.Vencimento = DateTime.Today.AddDays(10);
            parcela.Valor = 100m;
            nota.Parcelas.Add(parcela);
        }
        _db.NotasFiscais.Add(nota);
        await _db.SaveChangesAsync();
        return nota;
    }

    [Fact]
    public async Task Reprocessar_VoltaParaRecebidaEEnfileiraOLancamento()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Erro, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ReprocessarAsync(nota.Id, UsuarioDeTeste.DoCliente(1).HttpContext.User, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNota.Recebida, nota.Status);
        Assert.Equal(0, nota.Tentativas);
        Assert.Null(nota.MensagemErro);
        Assert.Single(_fila.Enfileirados);
    }

    [Fact]
    public async Task Reprocessar_NotaJaLancada_EhInvalido()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Lancada, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ReprocessarAsync(nota.Id, UsuarioDeTeste.Admin().HttpContext.User, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Empty(_fila.Enfileirados);
    }

    [Fact]
    public async Task Reprocessar_NotaSemParcelas_EhInvalido()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Erro, false);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ReprocessarAsync(nota.Id, UsuarioDeTeste.Admin().HttpContext.User, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Reprocessar_NotaDeOutroCliente_NaoEncontrada()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Erro, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ReprocessarAsync(nota.Id, UsuarioDeTeste.DoCliente(2).HttpContext.User, CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
        Assert.Equal(StatusNota.Erro, nota.Status);
        Assert.Empty(_fila.Enfileirados);
    }

    private static System.Security.Claims.ClaimsPrincipal UsuarioComNome(int clienteId, string nome)
    {
        System.Security.Claims.ClaimsPrincipal usuario = UsuarioDeTeste.DoCliente(clienteId).HttpContext.User;
        var identidade = (System.Security.Claims.ClaimsIdentity)usuario.Identity!;
        identidade.AddClaim(new System.Security.Claims.Claim(Previsio.Api.Autenticacao.ClaimsPrevisio.Nome, nome));
        return usuario;
    }

    [Fact]
    public async Task LancadaManualmente_GuardaQuemQuandoEObservacao()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Erro, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila).MarcarLancadaManualmenteAsync(
            nota.Id, "  lançada como conta 1234 ", UsuarioComNome(1, "Financeiro Alfa"), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNota.LancadaManualmente, nota.Status);
        Assert.Equal("Financeiro Alfa", nota.ResolvidaManualmentePor);
        Assert.Equal(2, nota.ResolvidaManualmentePorId);
        Assert.NotNull(nota.ResolvidaManualmenteEm);
        Assert.Equal("lançada como conta 1234", nota.ObservacaoResolucao);
        Assert.Empty(_fila.Enfileirados);
    }

    [Fact]
    public async Task LancadaManualmente_SaiDasPendentes()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.BlingDesconectado, true);
        NotasService servico = ServicosDeTeste.Notas(_db, _fila);

        await servico.MarcarLancadaManualmenteAsync(nota.Id, null, UsuarioComNome(1, "Financeiro"), CancellationToken.None);

        Assert.Empty(await servico.ListarPendentesAsync(1, null, CancellationToken.None));
        Assert.Null(nota.ObservacaoResolucao);
    }

    [Theory]
    [InlineData(StatusNota.Recebida)]
    [InlineData(StatusNota.Lancada)]
    [InlineData(StatusNota.LancadaManualmente)]
    public async Task LancadaManualmente_SoParaNotaComProblema(StatusNota status)
    {
        NotaFiscal nota = await CadastrarNota(status, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila).MarcarLancadaManualmenteAsync(
            nota.Id, null, UsuarioComNome(1, "Financeiro"), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Equal(status, nota.Status);
    }

    [Fact]
    public async Task LancadaManualmente_NotaDeOutroCliente_NaoEncontrada()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Erro, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila).MarcarLancadaManualmenteAsync(
            nota.Id, null, UsuarioComNome(2, "Outra rede"), CancellationToken.None);

        Assert.Equal(TipoResultado.NaoEncontrado, resultado.Tipo);
        Assert.Equal(StatusNota.Erro, nota.Status);
    }

    [Fact]
    public async Task LancadaManualmente_ObservacaoLongaDemais_EhInvalida()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.Erro, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila).MarcarLancadaManualmenteAsync(
            nota.Id, new string('x', 501), UsuarioComNome(1, "Financeiro"), CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Reprocessar_NotaLancadaManualmente_EhInvalido()
    {
        NotaFiscal nota = await CadastrarNota(StatusNota.LancadaManualmente, true);

        Resultado resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ReprocessarAsync(nota.Id, UsuarioDeTeste.Admin().HttpContext.User, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Empty(_fila.Enfileirados);
    }
}
