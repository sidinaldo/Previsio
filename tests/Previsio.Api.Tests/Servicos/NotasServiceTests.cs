using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;

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
}
