using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Aplicacao.Categorias;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.Categorias;

public class CategoriaLancamentoServiceTests
{
    private const long Contato = 555;
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private Cliente _rede = null!;
    private Empresa _empresa = null!;
    private NotaFiscal _nota = null!;

    private CategoriaLancamentoService CriarServico()
    {
        // As categorias já estão guardadas (de hoje): não busca no Bling
        var bling = BlingDeTeste.Criar(new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[]}")));
        return new CategoriaLancamentoService(
            new Repositorio<RegraCategoria>(_db),
            new Repositorio<Cliente>(_db),
            new Repositorio<ContaPagarSincronizada>(_db),
            new Repositorio<CategoriaFinanceira>(_db),
            new CategoriasBlingService(new Repositorio<CategoriaFinanceira>(_db), bling, NullLogger<CategoriasBlingService>.Instance));
    }

    private async Task Cadastrar(string? categoriaPadrao)
    {
        _rede = new Cliente { Nome = "Rede", CategoriaPadrao = categoriaPadrao };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _empresa = new Empresa { ClienteId = _rede.Id, Nome = "Loja", Cnpj = "11222333000181" };
        _db.Empresas.Add(_empresa);
        await _db.SaveChangesAsync();
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _empresa.Id, BlingId = 11, Descricao = "Fretes", Tipo = 1 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _empresa.Id, BlingId = 12, Descricao = "Compras de mercadorias", Tipo = 1 });
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _empresa.Id, BlingId = 20, Descricao = "Peças", Tipo = 2 });
        _nota = new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _empresa.Id, CnpjFornecedor = "12.345.678/0001-99", NomeFornecedor = "ROTA SUL" };
        await _db.SaveChangesAsync();
    }

    private Task<long?> Escolher()
    {
        return CriarServico().EscolherAsync(_nota, _empresa, Contato, "conta", "token", CancellationToken.None);
    }

    [Fact]
    public async Task Regra_da_rede_acha_a_categoria_pelo_nome_sem_ligar_para_acento_e_maiusculas()
    {
        await Cadastrar(null);
        _db.RegrasCategoria.Add(new RegraCategoria { ClienteId = _rede.Id, CnpjFornecedor = "12345678000199", Categoria = "  FRÉTES " });
        await _db.SaveChangesAsync();

        Assert.Equal(11, await Escolher());
    }

    [Fact]
    public async Task Regra_com_categoria_que_a_empresa_nao_tem_lanca_sem_categoria()
    {
        await Cadastrar("Compras de mercadorias");
        // "Peças" existe, mas é de receita
        _db.RegrasCategoria.Add(new RegraCategoria { ClienteId = _rede.Id, CnpjFornecedor = "12345678000199", Categoria = "Peças" });
        await _db.SaveChangesAsync();

        Assert.Null(await Escolher());
    }

    [Fact]
    public async Task Sem_regra_aprende_com_a_ultima_conta_do_fornecedor_no_Bling()
    {
        await Cadastrar("Compras de mercadorias");
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 1, BlingContatoId = Contato, CategoriaBlingId = 12, CategoriaConferidaEm = DateTime.UtcNow.AddDays(-5) });
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 2, BlingContatoId = Contato, CategoriaBlingId = 11, CategoriaConferidaEm = DateTime.UtcNow.AddDays(-1) });
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 3, BlingContatoId = 999, CategoriaBlingId = 12, CategoriaConferidaEm = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        Assert.Equal(11, await Escolher());
    }

    [Fact]
    public async Task Sem_regra_nem_historico_usa_a_categoria_padrao_da_rede()
    {
        await Cadastrar("compras de MERCADORIAS");

        Assert.Equal(12, await Escolher());
    }

    [Fact]
    public async Task Sem_nada_configurado_lanca_sem_categoria()
    {
        await Cadastrar(null);

        Assert.Null(await Escolher());
    }
}
