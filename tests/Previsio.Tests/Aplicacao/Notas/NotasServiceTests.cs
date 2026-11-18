using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Notas;

namespace Previsio.Tests.Aplicacao.Notas;

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
        identidade.AddClaim(new System.Security.Claims.Claim(Previsio.Aplicacao.Autenticacao.ClaimsPrevisio.Nome, nome));
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

    private NotaFiscal NotaLancada(Empresa empresa, string numero, params long?[] contas)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = 1;
        nota.EmpresaId = empresa.Id;
        nota.Numero = numero;
        nota.ChaveAcesso = numero.PadLeft(44, '9');
        nota.Status = StatusNota.Lancada;
        int n = 1;
        foreach (long? conta in contas)
        {
            nota.Parcelas.Add(new ParcelaNota { NumeroParcela = n.ToString("000"), Vencimento = DateTime.Today.AddDays(n), Valor = 10m, ErpContaPagarId = conta });
            n++;
        }
        _db.NotasFiscais.Add(nota);
        return nota;
    }

    private void Conta(Empresa empresa, long blingId, long? categoria, bool conferida)
    {
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = empresa.Id, ErpId = blingId, CategoriaErpId = categoria,
            CategoriaConferidaEm = conferida ? DateTime.UtcNow : null
        });
    }

    [Fact]
    public async Task Notas_recebidas_trazem_a_categoria_das_contas_no_Bling()
    {
        var empresa = new Empresa { ClienteId = 1, Nome = "Matriz", Cnpj = "11222333000181" };
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = empresa.Id, ErpId = 7, Descricao = "Compras de Mercadorias", Tipo = 1 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = empresa.Id, ErpId = 8, Descricao = "Fretes", Tipo = 1 });
        Conta(empresa, 101, 7, true);
        Conta(empresa, 102, 7, true);
        Conta(empresa, 201, null, true);
        Conta(empresa, 301, 7, false);
        Conta(empresa, 401, 7, true);
        Conta(empresa, 402, 8, true);
        Conta(empresa, 501, 99, true);
        NotaLancada(empresa, "1", 101, 102);
        NotaLancada(empresa, "2", 201);
        NotaLancada(empresa, "3", 301);
        NotaLancada(empresa, "4", 401, 402);
        NotaLancada(empresa, "5", 501);
        // Lançada, mas a conta ainda não chegou na sincronização
        NotaLancada(empresa, "6", 601);
        // Ainda não foi para o Bling
        NotaFiscal pendente = NotaLancada(empresa, "7", new long?[] { null });
        pendente.Status = StatusNota.Erro;
        await _db.SaveChangesAsync();

        var pagina = (await ServicosDeTeste.Notas(_db, _fila).ListarAsync(1, null, null, null, null, null, null, 1, 50, CancellationToken.None)).Valor!;

        var porNumero = pagina.Itens.ToDictionary(n => n.Numero);
        Assert.Equal("Compras de Mercadorias", porNumero["1"].Categoria);
        Assert.Equal("categoria", porNumero["1"].CategoriaTipo);
        Assert.Equal("Sem categoria", porNumero["2"].Categoria);
        Assert.Equal("sem", porNumero["2"].CategoriaTipo);
        Assert.Equal("A conferir", porNumero["3"].Categoria);
        Assert.Equal("conferir", porNumero["3"].CategoriaTipo);
        Assert.Equal("Várias categorias", porNumero["4"].Categoria);
        Assert.Equal("varias", porNumero["4"].CategoriaTipo);
        Assert.Equal("naoidentificada", porNumero["5"].CategoriaTipo);
        Assert.Equal("conferir", porNumero["6"].CategoriaTipo);
        Assert.Null(porNumero["7"].Categoria);
        Assert.Null(porNumero["7"].CategoriaTipo);
    }
}
