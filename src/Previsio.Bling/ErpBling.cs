using System.Globalization;
using Previsio.Aplicacao.Abstracoes.Erp;
using Previsio.Bling.Modelos;

namespace Previsio.Bling;

/// <summary>
/// O Bling atrás da interface de ERP do Previsio: converte os modelos da API v3 do Bling
/// para os modelos neutros da Aplicacao, e de volta.
/// </summary>
public class ErpBling : IErp
{
    private const string FormatoData = "yyyy-MM-dd";

    private readonly BlingClient _bling;

    public ErpBling(BlingClient bling)
    {
        _bling = bling;
    }

    public CredenciaisErp CredenciaisPadrao
    {
        get
        {
            CredenciaisBling padrao = _bling.CredenciaisPadrao;
            var credenciais = new CredenciaisErp();
            credenciais.ClientId = padrao.ClientId;
            credenciais.ClientSecret = padrao.ClientSecret;
            return credenciais;
        }
    }

    public string MontarUrlAutorizacao(CredenciaisErp credenciais, string estado)
    {
        return _bling.MontarUrlAutorizacao(ParaBling(credenciais), estado);
    }

    public async Task<TokenErp> TrocarCodigoPorTokenAsync(CredenciaisErp credenciais, string codigo, CancellationToken cancelamento)
    {
        TokenBling token = await _bling.TrocarCodigoPorTokenAsync(ParaBling(credenciais), codigo, cancelamento);
        return ParaErp(token);
    }

    public async Task<TokenErp> RenovarTokenAsync(CredenciaisErp credenciais, string refreshToken, CancellationToken cancelamento)
    {
        TokenBling token = await _bling.RenovarTokenAsync(ParaBling(credenciais), refreshToken, cancelamento);
        return ParaErp(token);
    }

    public async Task<ContatoErp?> BuscarContatoPorDocumentoAsync(
        string conta, string accessToken, string documento, CancellationToken cancelamento)
    {
        ContatoBling? contato = await _bling.BuscarContatoPorDocumentoAsync(conta, accessToken, documento, cancelamento);
        return ParaErp(contato);
    }

    public Task<long> CriarContatoAsync(string conta, string accessToken, NovoContatoErp contato, CancellationToken cancelamento)
    {
        var novo = new NovoContatoBling();
        novo.Nome = contato.Nome;
        novo.NumeroDocumento = contato.Documento;
        if (contato.Documento.Length == 11)
        {
            novo.Tipo = "F";
        }
        else
        {
            novo.Tipo = "J";
        }
        return _bling.CriarContatoAsync(conta, accessToken, novo, cancelamento);
    }

    public async Task<ContatoErp?> ObterContatoAsync(string conta, string accessToken, long id, CancellationToken cancelamento)
    {
        ContatoBling? contato = await _bling.ObterContatoAsync(conta, accessToken, id, cancelamento);
        return ParaErp(contato);
    }

    public Task<long> CriarContaPagarAsync(
        string conta, string accessToken, NovaContaPagarErp contaPagar, CancellationToken cancelamento)
    {
        var nova = new NovaContaPagarBling();
        nova.Vencimento = BlingClient.FormatarData(contaPagar.Vencimento);
        nova.Valor = contaPagar.Valor;
        nova.Contato = new Referencia { Id = contaPagar.ContatoId };
        nova.DataEmissao = BlingClient.FormatarData(contaPagar.DataEmissao);
        nova.Competencia = BlingClient.FormatarData(contaPagar.Competencia);
        nova.NumeroDocumento = contaPagar.NumeroDocumento;
        nova.Historico = contaPagar.Historico;
        if (contaPagar.CategoriaId.HasValue)
        {
            nova.Categoria = new Referencia { Id = contaPagar.CategoriaId.Value };
        }
        if (contaPagar.FormaPagamentoId.HasValue)
        {
            nova.FormaPagamento = new Referencia { Id = contaPagar.FormaPagamentoId.Value };
        }
        return _bling.CriarContaPagarAsync(conta, accessToken, nova, cancelamento);
    }

    public async Task<List<ContaPagarErp>> ListarContasPagarAsync(
        string conta, string accessToken, DateTime vencimentoInicial, DateTime vencimentoFinal, CancellationToken cancelamento)
    {
        var filtro = new FiltroContasPagar();
        filtro.VencimentoInicial = vencimentoInicial;
        filtro.VencimentoFinal = vencimentoFinal;
        List<ContaPagarBling> contas = await _bling.ListarContasPagarAsync(conta, accessToken, filtro, cancelamento);

        var lista = new List<ContaPagarErp>();
        foreach (ContaPagarBling item in contas)
        {
            lista.Add(ParaErp(item));
        }
        return lista;
    }

    public async Task<ContaPagarErp?> ObterContaPagarAsync(string conta, string accessToken, long id, CancellationToken cancelamento)
    {
        ContaPagarBling? item = await _bling.ObterContaPagarAsync(conta, accessToken, id, cancelamento);
        if (item == null)
        {
            return null;
        }
        return ParaErp(item);
    }

    public async Task<List<ContaReceberErp>> ListarContasReceberAsync(
        string conta, string accessToken, DateTime vencimentoInicial, DateTime vencimentoFinal, CancellationToken cancelamento)
    {
        List<ContaReceberBling> contas = await _bling.ListarContasReceberAsync(
            conta, accessToken, vencimentoInicial, vencimentoFinal, cancelamento);

        var lista = new List<ContaReceberErp>();
        foreach (ContaReceberBling item in contas)
        {
            var receber = new ContaReceberErp();
            receber.Id = item.Id;
            receber.Situacao = item.Situacao;
            receber.Vencimento = LerData(item.Vencimento);
            receber.Valor = item.Valor;
            if (item.Contato != null)
            {
                receber.ContatoId = item.Contato.Id;
                receber.ContatoNome = item.Contato.Nome;
            }
            lista.Add(receber);
        }
        return lista;
    }

    public async Task<List<CategoriaErp>> ListarCategoriasAsync(string conta, string accessToken, CancellationToken cancelamento)
    {
        List<CategoriaBling> categorias = await _bling.ListarCategoriasAsync(conta, accessToken, cancelamento);

        var lista = new List<CategoriaErp>();
        foreach (CategoriaBling item in categorias)
        {
            var categoria = new CategoriaErp();
            categoria.Id = item.Id;
            categoria.IdPai = item.IdCategoriaPai;
            categoria.Descricao = item.Descricao;
            categoria.Tipo = item.Tipo;
            lista.Add(categoria);
        }
        return lista;
    }

    public async Task<List<FormaPagamentoErp>> ListarFormasPagamentoAsync(
        string conta, string accessToken, CancellationToken cancelamento)
    {
        List<FormaPagamentoBling> formas = await _bling.ListarFormasPagamentoAsync(conta, accessToken, cancelamento);

        var lista = new List<FormaPagamentoErp>();
        foreach (FormaPagamentoBling item in formas)
        {
            var forma = new FormaPagamentoErp();
            forma.Id = item.Id;
            forma.Descricao = item.Descricao;
            forma.TipoPagamento = item.TipoPagamento;
            forma.Situacao = item.Situacao;
            lista.Add(forma);
        }
        return lista;
    }

    private static CredenciaisBling ParaBling(CredenciaisErp credenciais)
    {
        var bling = new CredenciaisBling();
        bling.ClientId = credenciais.ClientId;
        bling.ClientSecret = credenciais.ClientSecret;
        return bling;
    }

    private static TokenErp ParaErp(TokenBling token)
    {
        var erp = new TokenErp();
        erp.AccessToken = token.AccessToken;
        erp.RefreshToken = token.RefreshToken;
        erp.ExpiraEmSegundos = token.ExpiraEmSegundos;
        return erp;
    }

    private static ContatoErp? ParaErp(ContatoBling? contato)
    {
        if (contato == null)
        {
            return null;
        }
        var erp = new ContatoErp();
        erp.Id = contato.Id;
        erp.Nome = contato.Nome;
        erp.Documento = contato.NumeroDocumento;
        return erp;
    }

    private static ContaPagarErp ParaErp(ContaPagarBling item)
    {
        var conta = new ContaPagarErp();
        conta.Id = item.Id;
        conta.Situacao = item.Situacao;
        conta.Vencimento = LerData(item.Vencimento);
        conta.Valor = item.Valor;
        conta.NumeroDocumento = item.NumeroDocumento;
        conta.Historico = item.Historico;
        if (item.Contato != null)
        {
            conta.ContatoId = item.Contato.Id;
        }
        if (item.Categoria != null)
        {
            conta.CategoriaId = item.Categoria.Id;
        }
        if (item.FormaPagamento != null)
        {
            conta.FormaPagamentoId = item.FormaPagamento.Id;
        }
        return conta;
    }

    // O Bling manda as datas como "AAAA-MM-DD"; nulo quando não dá para ler
    private static DateTime? LerData(string texto)
    {
        DateTime data;
        if (DateTime.TryParseExact(texto, FormatoData, CultureInfo.InvariantCulture, DateTimeStyles.None, out data))
        {
            return data.Date;
        }
        return null;
    }
}
