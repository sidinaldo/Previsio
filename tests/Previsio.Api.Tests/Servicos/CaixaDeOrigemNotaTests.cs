using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Tests.Servicos;

/// <summary>
/// A nota vai para a empresa pelo CNPJ do XML; a caixa em que chegou só serve para avisar quando não bate.
/// </summary>
public class CaixaDeOrigemNotaTests
{
    private const string CnpjMatriz = "11222333000181";
    private const string CnpjFilial = "11222333000262";

    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly FilaDeJobsDeTeste _fila = new FilaDeJobsDeTeste();
    private Empresa _matriz = null!;
    private Empresa _filial = null!;

    private async Task CadastrarEmpresas()
    {
        _matriz = new Empresa();
        _matriz.ClienteId = 1;
        _matriz.Nome = "Matriz";
        _matriz.Cnpj = CnpjMatriz;
        _filial = new Empresa();
        _filial.ClienteId = 1;
        _filial.Nome = "Filial 2";
        _filial.Cnpj = CnpjFilial;
        _db.Empresas.Add(_matriz);
        _db.Empresas.Add(_filial);
        await _db.SaveChangesAsync();
    }

    private static NotaFiscalXml Xml(string chave, string cnpjDestinatario)
    {
        var xml = new NotaFiscalXml();
        xml.ChaveAcesso = chave;
        xml.Numero = "48213";
        xml.Serie = "1";
        xml.DataEmissao = DateTime.Today;
        xml.ValorTotal = 907m;
        xml.CnpjFornecedor = "45987123000104";
        xml.NomeFornecedor = "ALFA DISTRIBUIDORA DE AUTOPECAS LTDA";
        xml.CnpjDestinatario = cnpjDestinatario;
        var duplicata = new DuplicataXml();
        duplicata.Numero = "001";
        duplicata.Vencimento = DateTime.Today.AddDays(6);
        duplicata.Valor = 907m;
        xml.Duplicatas.Add(duplicata);
        return xml;
    }

    private async Task<NotaDto> Registrar(string chave, string cnpjDestinatario, int? empresaDaCaixaId)
    {
        var registro = ServicosDeTeste.RegistroNotaService(_db);
        await registro.RegistrarAsync(1, Xml(chave, cnpjDestinatario), "fornecedor@alfa.com.br", empresaDaCaixaId, CancellationToken.None);
        _db.ChangeTracker.Clear();

        List<NotaDto> pendentes = await ServicosDeTeste.Notas(_db, _fila).ListarPendentesAsync(1, null, CancellationToken.None);
        return pendentes.Single(n => n.ChaveAcesso == chave);
    }

    [Fact]
    public async Task NotaDaMatrizNaCaixaDaFilial_VaiParaAMatrizEAvisaACaixa()
    {
        await CadastrarEmpresas();

        NotaDto nota = await Registrar("35261145987123000104550010000482131819034769", CnpjMatriz, _filial.Id);

        Assert.Equal(_matriz.Id, nota.EmpresaId);
        Assert.Equal("Filial 2", nota.CaixaDeOutraEmpresa);
    }

    [Fact]
    public async Task NotaNaCaixaDaPropriaEmpresa_SemAviso()
    {
        await CadastrarEmpresas();

        NotaDto nota = await Registrar("35261145987123000104550010000482131819034770", CnpjFilial, _filial.Id);

        Assert.Equal(_filial.Id, nota.EmpresaId);
        Assert.Null(nota.CaixaDeOutraEmpresa);
    }

    [Fact]
    public async Task NotaNaCaixaDaRede_SemAviso()
    {
        await CadastrarEmpresas();

        NotaDto nota = await Registrar("35261145987123000104550010000482131819034771", CnpjMatriz, null);

        Assert.Equal(_matriz.Id, nota.EmpresaId);
        Assert.Null(nota.CaixaDeOutraEmpresa);
    }

    [Fact]
    public async Task CnpjNaoCadastradoNaCaixaDaFilial_AvisaACaixa()
    {
        await CadastrarEmpresas();

        NotaDto nota = await Registrar("35261145987123000104550010000482131819034772", "11222333000343", _filial.Id);

        Assert.Null(nota.EmpresaId);
        Assert.Equal("EmpresaNaoEncontrada", nota.Status);
        Assert.Equal("Filial 2", nota.CaixaDeOutraEmpresa);
    }
}
