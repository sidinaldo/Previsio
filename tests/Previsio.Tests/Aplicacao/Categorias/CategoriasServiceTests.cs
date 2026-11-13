using Microsoft.Extensions.Options;
using Previsio.Aplicacao.Categorias;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.Categorias;

public class CategoriasServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private Cliente _rede = null!;
    private Empresa _centro = null!;
    private Empresa _norte = null!;
    private Empresa _semCategorias = null!;

    private CategoriasService Servico()
    {
        return new CategoriasService(
            new Repositorio<Cliente>(_db),
            new Repositorio<Empresa>(_db),
            new Repositorio<CategoriaFinanceira>(_db),
            new Repositorio<RegraCategoria>(_db),
            new Repositorio<NotaFiscal>(_db),
            Options.Create(new CategoriasOptions()));
    }

    private async Task Cadastrar()
    {
        _rede = new Cliente { Nome = "Rede" };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _centro = Empresa("Centro");
        _norte = Empresa("Norte");
        _semCategorias = Empresa("Sul");
        Empresa desconectada = Empresa("Desconectada");
        desconectada.BlingRefreshTokenProtegido = null;
        await _db.SaveChangesAsync();
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _centro.Id, BlingId = 1, Descricao = "Fretes", Tipo = 1 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _centro.Id, BlingId = 2, Descricao = "Compras", Tipo = 1 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _norte.Id, BlingId = 3, Descricao = "FRETES", Tipo = 1 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _norte.Id, BlingId = 4, Descricao = "Vendas", Tipo = 2 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = desconectada.Id, BlingId = 5, Descricao = "Aluguel", Tipo = 1 });
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _centro.Id, CnpjFornecedor = "12.345.678/0001-99", NomeFornecedor = "ROTA SUL" });
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _norte.Id, CnpjFornecedor = "12345678000199", NomeFornecedor = "ROTA SUL LTDA" });
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _norte.Id, CnpjFornecedor = "98765432000155", NomeFornecedor = "AUTOPEÇAS ABC" });
        await _db.SaveChangesAsync();
    }

    private Empresa Empresa(string nome)
    {
        var empresa = new Empresa { ClienteId = _rede.Id, Nome = nome, Cnpj = "11222333000181", BlingRefreshTokenProtegido = "token" };
        _db.Empresas.Add(empresa);
        return empresa;
    }

    [Fact]
    public async Task Mostra_as_opcoes_de_despesa_das_empresas_conectadas_e_os_fornecedores()
    {
        await Cadastrar();

        Resultado<ConfiguracaoCategoriasDto> resultado = await Servico().ObterAsync(_rede.Id, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        ConfiguracaoCategoriasDto dto = resultado.Valor!;
        Assert.Equal(new[] { "Compras", "Fretes" }, dto.Opcoes.ToArray());
        Assert.Equal(new[] { "Sul" }, dto.EmpresasSemCategorias.ToArray());
        Assert.Equal(2, dto.Fornecedores.Count);
        Assert.Contains(dto.Fornecedores, f => f.Cnpj == "12345678000199");
    }

    [Fact]
    public async Task Regra_guarda_o_nome_do_Bling_troca_a_categoria_e_avisa_onde_falta()
    {
        await Cadastrar();
        CategoriasService servico = Servico();

        await servico.SalvarRegraAsync(_rede.Id, new NovaRegraCategoriaDto { CnpjFornecedor = "12.345.678/0001-99", Categoria = "fretes" }, CancellationToken.None);
        Resultado<ConfiguracaoCategoriasDto> resultado = await servico.SalvarRegraAsync(
            _rede.Id, new NovaRegraCategoriaDto { CnpjFornecedor = "12345678000199", Categoria = "compras" }, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        RegraCategoriaDto regra = Assert.Single(resultado.Valor!.Regras);
        Assert.Equal("Compras", regra.Categoria);
        Assert.Equal("12345678000199", regra.CnpjFornecedor);
        Assert.NotNull(regra.FornecedorNome);
        Assert.Equal(new[] { "Norte" }, regra.AusenteEm.ToArray());
        Assert.Single(_db.RegrasCategoria);
    }

    [Fact]
    public async Task Recusa_regra_com_categoria_inexistente_de_receita_ou_cnpj_invalido()
    {
        await Cadastrar();
        CategoriasService servico = Servico();

        Resultado<ConfiguracaoCategoriasDto> receita = await servico.SalvarRegraAsync(
            _rede.Id, new NovaRegraCategoriaDto { CnpjFornecedor = "12345678000199", Categoria = "Vendas" }, CancellationToken.None);
        Resultado<ConfiguracaoCategoriasDto> desconectada = await servico.SalvarRegraAsync(
            _rede.Id, new NovaRegraCategoriaDto { CnpjFornecedor = "12345678000199", Categoria = "Aluguel" }, CancellationToken.None);
        Resultado<ConfiguracaoCategoriasDto> cnpj = await servico.SalvarRegraAsync(
            _rede.Id, new NovaRegraCategoriaDto { CnpjFornecedor = "123", Categoria = "Fretes" }, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, receita.Tipo);
        Assert.Equal(TipoResultado.Invalido, desconectada.Tipo);
        Assert.Equal(TipoResultado.Invalido, cnpj.Tipo);
        Assert.Empty(_db.RegrasCategoria);
    }

    [Fact]
    public async Task Categoria_padrao_guarda_o_nome_e_vazio_tira()
    {
        await Cadastrar();
        CategoriasService servico = Servico();

        Resultado<ConfiguracaoCategoriasDto> resultado = await servico.AlterarPadraoAsync(
            _rede.Id, new CategoriaPadraoDto { Categoria = " fretes " }, CancellationToken.None);
        Assert.Equal("Fretes", resultado.Valor!.CategoriaPadrao);
        Assert.Empty(resultado.Valor.PadraoAusenteEm);

        resultado = await servico.AlterarPadraoAsync(_rede.Id, new CategoriaPadraoDto { Categoria = "" }, CancellationToken.None);
        Assert.Null(resultado.Valor!.CategoriaPadrao);
    }

    [Fact]
    public async Task Remove_regra_so_da_propria_rede()
    {
        await Cadastrar();
        var outra = new Cliente { Nome = "Outra" };
        _db.Clientes.Add(outra);
        await _db.SaveChangesAsync();
        var regra = new RegraCategoria { ClienteId = _rede.Id, CnpjFornecedor = "12345678000199", Categoria = "Fretes" };
        _db.RegrasCategoria.Add(regra);
        await _db.SaveChangesAsync();
        CategoriasService servico = Servico();

        Resultado<ConfiguracaoCategoriasDto> deOutra = await servico.RemoverRegraAsync(outra.Id, regra.Id, CancellationToken.None);
        Assert.Equal(TipoResultado.NaoEncontrado, deOutra.Tipo);

        Resultado<ConfiguracaoCategoriasDto> removida = await servico.RemoverRegraAsync(_rede.Id, regra.Id, CancellationToken.None);
        Assert.True(removida.Sucesso);
        Assert.Empty(_db.RegrasCategoria);
    }
}
