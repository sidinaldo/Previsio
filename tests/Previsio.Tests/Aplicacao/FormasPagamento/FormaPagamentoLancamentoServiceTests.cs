using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao.FormasPagamento;
using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.FormasPagamento;

public class FormaPagamentoLancamentoServiceTests
{
    private const long Contato = 555;
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private Cliente _rede = null!;
    private Empresa _empresa = null!;
    private NotaFiscal _nota = null!;

    private FormaPagamentoLancamentoService CriarServico(FormasPagamentoOptions? opcoes = null)
    {
        // As formas já estão guardadas (de hoje): não busca no Bling
        var bling = BlingDeTeste.Criar(new RespostasFixas(_ => RespostasFixas.Json(HttpStatusCode.OK, "{\"data\":[]}")));
        FormasPagamentoOptions escolhidas = opcoes ?? new FormasPagamentoOptions();
        return new FormaPagamentoLancamentoService(
            new Repositorio<RegraFormaPagamento>(_db),
            new Repositorio<Cliente>(_db),
            new Repositorio<ContaPagarSincronizada>(_db),
            new Repositorio<FormaPagamento>(_db),
            new FormasPagamentoBlingService(new Repositorio<FormaPagamento>(_db), bling, Options.Create(escolhidas),
                NullLogger<FormasPagamentoBlingService>.Instance),
            Options.Create(escolhidas));
    }

    private void Forma(long id, string nome, int tipo, string grupo, int situacao = 1)
    {
        _db.FormasPagamento.Add(new FormaPagamento
        {
            EmpresaId = _empresa.Id, BlingId = id, Descricao = nome, TipoPagamento = tipo, Situacao = situacao, Grupo = grupo
        });
    }

    private async Task Cadastrar(string? formaPadrao, int? tipoNaNota)
    {
        _rede = new Cliente { Nome = "Rede", FormaPagamentoPadrao = formaPadrao };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _empresa = new Empresa { ClienteId = _rede.Id, Nome = "Loja", Cnpj = "11222333000181" };
        _db.Empresas.Add(_empresa);
        await _db.SaveChangesAsync();
        Forma(1, "Boleto", 15, GruposFormaPagamento.Boleto);
        Forma(2, "Pix", 17, GruposFormaPagamento.Pix);
        Forma(3, "Débito automático", 15, GruposFormaPagamento.DebitoAutomatico);
        Forma(4, "Transferência", 18, GruposFormaPagamento.Transferencia);
        Forma(5, "Cheque antigo", 2, GruposFormaPagamento.Outras, situacao: 2);
        _nota = new NotaFiscal
        {
            ClienteId = _rede.Id, EmpresaId = _empresa.Id, CnpjFornecedor = "12.345.678/0001-99", NomeFornecedor = "ROTA SUL",
            TipoPagamento = tipoNaNota
        };
        await _db.SaveChangesAsync();
    }

    private Task<long?> Escolher(FormasPagamentoOptions? opcoes = null)
    {
        return CriarServico(opcoes).EscolherAsync(_nota, _empresa, Contato, "conta", "token", CancellationToken.None);
    }

    [Fact]
    public async Task Regra_da_rede_vale_antes_de_tudo_pelo_nome()
    {
        await Cadastrar("Pix", 15);
        _db.RegrasFormaPagamento.Add(new RegraFormaPagamento { ClienteId = _rede.Id, CnpjFornecedor = "12345678000199", Forma = " DEBITO AUTOMATICO " });
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 70, BlingContatoId = Contato, FormaPagamentoBlingId = 4 });
        await _db.SaveChangesAsync();

        Assert.Equal(3, await Escolher());
    }

    [Fact]
    public async Task Regra_com_forma_que_a_empresa_nao_tem_segue_para_o_historico()
    {
        await Cadastrar(null, null);
        // "Cheque antigo" existe, mas está inativo no Bling
        _db.RegrasFormaPagamento.Add(new RegraFormaPagamento { ClienteId = _rede.Id, CnpjFornecedor = "12345678000199", Forma = "Cheque antigo" });
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 70, BlingContatoId = Contato, FormaPagamentoBlingId = 4 });
        await _db.SaveChangesAsync();

        Assert.Equal(4, await Escolher());
    }

    [Fact]
    public async Task Historico_usa_a_ultima_conta_do_fornecedor_com_forma_que_ainda_existe()
    {
        await Cadastrar(null, 15);
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 70, BlingContatoId = Contato, Vencimento = new DateTime(2026, 10, 1), FormaPagamentoBlingId = 2 });
        // A mais recente tem uma forma que não está mais no Bling: vale a anterior
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 71, BlingContatoId = Contato, Vencimento = new DateTime(2026, 11, 1), FormaPagamentoBlingId = 999 });
        // Outro fornecedor não conta
        _db.ContasPagar.Add(new ContaPagarSincronizada { EmpresaId = _empresa.Id, BlingId = 72, BlingContatoId = 888, Vencimento = new DateTime(2026, 12, 1), FormaPagamentoBlingId = 4 });
        await _db.SaveChangesAsync();

        Assert.Equal(2, await Escolher());
    }

    [Fact]
    public async Task Sem_regra_nem_historico_usa_o_tipo_do_xml_sem_cair_no_debito_automatico()
    {
        // Boleto (15) no XML: "Débito automático" também é tipo 15, mas não é escolhido pelo XML
        await Cadastrar("Pix", 15);

        Assert.Equal(1, await Escolher());
    }

    [Fact]
    public async Task Tipo_do_xml_que_a_empresa_nao_tem_usa_a_padrao_da_rede()
    {
        await Cadastrar("pix", 1);

        Assert.Equal(2, await Escolher());
    }

    [Fact]
    public async Task Rede_sem_padrao_e_nota_sem_tipo_usa_o_boleto_automatico()
    {
        await Cadastrar(null, null);

        Assert.Equal(1, await Escolher());
    }

    [Fact]
    public async Task Com_a_automatica_desligada_lanca_sem_forma()
    {
        await Cadastrar(null, null);

        Assert.Null(await Escolher(new FormasPagamentoOptions { PadraoAutomatico = "" }));
    }
}
