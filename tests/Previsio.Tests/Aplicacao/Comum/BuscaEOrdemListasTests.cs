using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Aplicacao.Comum;

/// <summary>
/// Caixa de busca (NF, fornecedor, empresa, CNPJ) e ordenação por coluna em Notas recebidas e Falhas.
/// </summary>
public class BuscaEOrdemListasTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly FilaDeJobsDeTeste _fila = new FilaDeJobsDeTeste();

    private async Task CadastrarNotas()
    {
        var zonaNorte = new Empresa();
        zonaNorte.ClienteId = 1;
        zonaNorte.Nome = "Auto Center Zona Norte";
        zonaNorte.Cnpj = "11222333000181";
        var centro = new Empresa();
        centro.ClienteId = 1;
        centro.Nome = "Auto Center Centro";
        centro.Cnpj = "11222333000262";
        _db.Empresas.Add(zonaNorte);
        _db.Empresas.Add(centro);
        await _db.SaveChangesAsync();

        _db.NotasFiscais.Add(Nota(zonaNorte.Id, "48213", "ALFA DISTRIBUIDORA DE AUTOPECAS LTDA", "45987123000104", 907m, 1));
        _db.NotasFiscais.Add(Nota(centro.Id, "900", "KAPPA SUSPENSOES AUTOMOTIVAS LTDA", "71295348000100", 1290m, 2));
        _db.NotasFiscais.Add(Nota(centro.Id, "82002", "ETA ESCAPAMENTOS E SILENCIOSOS LTDA", "80426153000134", 2247.40m, 3));
        await _db.SaveChangesAsync();
    }

    private static NotaFiscal Nota(int empresaId, string numero, string fornecedor, string cnpj, decimal valor, int ordem)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = 1;
        nota.EmpresaId = empresaId;
        nota.Numero = numero;
        nota.ChaveAcesso = "3526" + cnpj + numero.PadLeft(26, '0');
        nota.NomeFornecedor = fornecedor;
        nota.CnpjFornecedor = cnpj;
        nota.CnpjDestinatario = "11222333000181";
        nota.ValorTotal = valor;
        nota.Status = StatusNota.Lancada;
        nota.RecebidaEm = new DateTime(2026, 11, 11, 12, ordem, 0, DateTimeKind.Utc);
        return nota;
    }

    private async Task<List<string>> Numeros(string? busca, string? ordem)
    {
        Resultado<PaginaNotasDto> resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ListarAsync(1, null, null, null, null, busca, ordem, 1, CancellationToken.None);
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        return resultado.Valor!.Itens.Select(n => n.Numero).ToList();
    }

    [Fact]
    public async Task Busca_PeloNumeroDaNota()
    {
        await CadastrarNotas();
        Assert.Equal(new List<string> { "48213" }, await Numeros("4821", null));
    }

    [Fact]
    public async Task Busca_PeloFornecedor_SemDiferenciarMaiusculas()
    {
        await CadastrarNotas();
        Assert.Equal(new List<string> { "900" }, await Numeros("kappa", null));
    }

    [Fact]
    public async Task Busca_PeloCnpjComPontuacao()
    {
        await CadastrarNotas();
        Assert.Equal(new List<string> { "82002" }, await Numeros("80.426.153/0001-34", null));
    }

    [Fact]
    public async Task Busca_PeloNomeDaEmpresa()
    {
        await CadastrarNotas();
        List<string> numeros = await Numeros("centro", "numero");
        Assert.Equal(new List<string> { "900", "82002" }, numeros);
    }

    [Fact]
    public async Task SemOrdem_DaRecebidaMaisRecenteParaAMaisAntiga()
    {
        await CadastrarNotas();
        Assert.Equal(new List<string> { "82002", "900", "48213" }, await Numeros(null, null));
    }

    [Fact]
    public async Task Ordem_PorValorCrescenteEDecrescente()
    {
        await CadastrarNotas();
        Assert.Equal(new List<string> { "48213", "900", "82002" }, await Numeros(null, "valor"));
        Assert.Equal(new List<string> { "82002", "900", "48213" }, await Numeros(null, "-valor"));
    }

    [Fact]
    public async Task Ordem_PorNumero_ComoNumeroENaoComoTexto()
    {
        await CadastrarNotas();
        Assert.Equal(new List<string> { "900", "48213", "82002" }, await Numeros(null, "numero"));
    }

    [Fact]
    public async Task Ordem_ColunaInexistente_EhInvalida()
    {
        await CadastrarNotas();
        Resultado<PaginaNotasDto> resultado = await ServicosDeTeste.Notas(_db, _fila)
            .ListarAsync(1, null, null, null, null, null, "senha", 1, CancellationToken.None);
        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Falhas_BuscaPeloFornecedorEOrdenaPorValor()
    {
        _db.Falhas.Add(Falha("26", "OMEGA PECAS COM CNPJ INVALIDO LTDA", 319.80m));
        _db.Falhas.Add(Falha("27", "OMEGA PECAS COM CNPJ INVALIDO LTDA", 120m));
        _db.Falhas.Add(Falha("28", "BETA LUBRIFICANTES", 50m));
        await _db.SaveChangesAsync();

        Resultado<PaginaFalhasDto> resultado = await ServicosDeTeste.Falhas(_db)
            .ListarAsync(1, null, null, null, null, null, "omega", "valor", 1, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal(new List<string?> { "27", "26" }, resultado.Valor!.Itens.Select(f => f.NumeroNota).ToList());
    }

    private static FalhaProcessamento Falha(string numero, string fornecedor, decimal valor)
    {
        var falha = new FalhaProcessamento();
        falha.ClienteId = 1;
        falha.Etapa = EtapaFalha.CadastroFornecedor;
        falha.Mensagem = "Bling recusou";
        falha.NumeroNota = numero;
        falha.Fornecedor = fornecedor;
        falha.CnpjFornecedor = "45987123000100";
        falha.ValorNota = valor;
        return falha;
    }
}
