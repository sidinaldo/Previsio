using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.FormasPagamento;
using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;
using Previsio.Data;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.FormasPagamento;

public class FormasPagamentoServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private Cliente _rede = null!;
    private Empresa _centro = null!;
    private Empresa _norte = null!;

    private FormasPagamentoService Servico()
    {
        return ServicosDeTeste.FormasPagamentoService(_db);
    }

    private Empresa Empresa(string nome)
    {
        var empresa = new Empresa { ClienteId = _rede.Id, Nome = nome, Cnpj = "11222333000181", BlingRefreshTokenProtegido = "x" };
        _db.Empresas.Add(empresa);
        return empresa;
    }

    private void Forma(Empresa empresa, long id, string nome, int tipo, string grupo, int situacao = 1)
    {
        _db.FormasPagamento.Add(new FormaPagamento
        {
            EmpresaId = empresa.Id, BlingId = id, Descricao = nome, TipoPagamento = tipo, Situacao = situacao, Grupo = grupo
        });
    }

    private async Task Cadastrar()
    {
        _rede = new Cliente { Nome = "Rede" };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _centro = Empresa("Centro");
        _norte = Empresa("Norte");
        await _db.SaveChangesAsync();
        Forma(_centro, 1, "Boleto", 15, GruposFormaPagamento.Boleto);
        Forma(_centro, 2, "Débito automático", 99, GruposFormaPagamento.DebitoAutomatico);
        Forma(_norte, 10, "boleto", 15, GruposFormaPagamento.Boleto);
        Forma(_norte, 11, "Pix", 17, GruposFormaPagamento.Pix);
        Forma(_norte, 12, "Cheque", 2, GruposFormaPagamento.Outras, situacao: 2);
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, CnpjFornecedor = "12345678000199", NomeFornecedor = "ROTA SUL", ChaveAcesso = "1" });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Obter_traz_as_formas_ativas_da_rede_com_grupo_e_o_padrao_automatico()
    {
        await Cadastrar();

        ConfiguracaoFormasPagamentoDto dto = (await Servico().ObterAsync(_rede.Id, CancellationToken.None)).Valor!;

        Assert.Equal(new[] { "Boleto", "Débito automático", "Pix" }, dto.Opcoes.Select(o => o.Nome).ToArray());
        OpcaoFormaPagamentoDto debito = dto.Opcoes.Single(o => o.Nome == "Débito automático");
        Assert.True(debito.SaiSozinho);
        Assert.Equal("Débito automático", debito.GrupoNome);
        Assert.False(dto.Opcoes.Single(o => o.Nome == "Boleto").SaiSozinho);
        Assert.Null(dto.FormaPadrao);
        Assert.Equal("Boleto", dto.PadraoAutomatico);
        // As duas têm Boleto (sem diferença de maiúsculas)
        Assert.Empty(dto.PadraoAusenteEm);
        Assert.Equal(new[] { "Débito automático" }, dto.GruposSaiSozinho.ToArray());
        Assert.Single(dto.Fornecedores);
    }

    [Fact]
    public async Task Padrao_e_regra_so_aceitam_forma_que_existe_e_avisam_onde_falta()
    {
        await Cadastrar();
        FormasPagamentoService servico = Servico();

        Resultado<ConfiguracaoFormasPagamentoDto> invalida = await servico.AlterarPadraoAsync(
            _rede.Id, new FormaPagamentoPadraoDto { Forma = "Cheque" }, CancellationToken.None);
        Assert.False(invalida.Sucesso);

        ConfiguracaoFormasPagamentoDto comPadrao = (await servico.AlterarPadraoAsync(
            _rede.Id, new FormaPagamentoPadraoDto { Forma = "PIX" }, CancellationToken.None)).Valor!;
        Assert.Equal("Pix", comPadrao.FormaPadrao);
        Assert.Equal(new[] { "Centro" }, comPadrao.PadraoAusenteEm.ToArray());

        ConfiguracaoFormasPagamentoDto comRegra = (await servico.SalvarRegraAsync(_rede.Id,
            new NovaRegraFormaPagamentoDto { CnpjFornecedor = "12.345.678/0001-99", Forma = "debito automatico" }, CancellationToken.None)).Valor!;
        RegraFormaPagamentoDto regra = Assert.Single(comRegra.Regras);
        Assert.Equal("Débito automático", regra.Forma);
        Assert.Equal("ROTA SUL", regra.FornecedorNome);
        Assert.Equal(new[] { "Norte" }, regra.AusenteEm.ToArray());

        // Mesmo fornecedor: troca a forma
        await servico.SalvarRegraAsync(_rede.Id, new NovaRegraFormaPagamentoDto { CnpjFornecedor = "12345678000199", Forma = "Boleto" }, CancellationToken.None);
        Assert.Equal("Boleto", _db.RegrasFormaPagamento.Single().Forma);

        ConfiguracaoFormasPagamentoDto semRegra = (await servico.RemoverRegraAsync(_rede.Id, regra.Id, CancellationToken.None)).Valor!;
        Assert.Empty(semRegra.Regras);

        ConfiguracaoFormasPagamentoDto semPadrao = (await servico.AlterarPadraoAsync(
            _rede.Id, new FormaPagamentoPadraoDto { Forma = " " }, CancellationToken.None)).Valor!;
        Assert.Null(semPadrao.FormaPadrao);
    }
}
