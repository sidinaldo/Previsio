using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Previsio.Bling.Modelos;

namespace Previsio.Bling;

/// <summary>
/// Cliente da API v3 do Bling (https://developer.bling.com.br/referencia).
/// Cada chamada recebe o identificador da conta (para o limite de requisições)
/// e o access token daquela conta.
/// </summary>
public class BlingClient
{
    private static readonly JsonSerializerOptions OpcoesJson = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly BlingOptions _opcoes;
    private readonly LimitadorRequisicoes _limitador;
    private readonly ILogger<BlingClient> _logger;

    public BlingClient(
        HttpClient http,
        IOptions<BlingOptions> opcoes,
        LimitadorRequisicoes limitador,
        ILogger<BlingClient> logger)
    {
        _http = http;
        _opcoes = opcoes.Value;
        _limitador = limitador;
        _logger = logger;
        _http.BaseAddress = new Uri(_opcoes.ApiBaseUrl);
    }

    // ---------------------------------------------------------------
    // OAuth
    // ---------------------------------------------------------------

    /// <summary>
    /// Aplicativo global do Previsio, configurado no appsettings (seção "Bling").
    /// </summary>
    public CredenciaisBling CredenciaisPadrao
    {
        get
        {
            var credenciais = new CredenciaisBling();
            credenciais.ClientId = _opcoes.ClientId;
            credenciais.ClientSecret = _opcoes.ClientSecret;
            return credenciais;
        }
    }

    public string MontarUrlAutorizacao(CredenciaisBling credenciais, string state)
    {
        return _opcoes.AuthorizeUrl
            + "?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(credenciais.ClientId)
            + "&state=" + Uri.EscapeDataString(state);
    }

    public Task<TokenBling> TrocarCodigoPorTokenAsync(
        CredenciaisBling credenciais, string codigo, CancellationToken cancelamento)
    {
        var campos = new Dictionary<string, string>();
        campos["grant_type"] = "authorization_code";
        campos["code"] = codigo;
        return SolicitarTokenAsync(credenciais, campos, cancelamento);
    }

    public Task<TokenBling> RenovarTokenAsync(
        CredenciaisBling credenciais, string refreshToken, CancellationToken cancelamento)
    {
        var campos = new Dictionary<string, string>();
        campos["grant_type"] = "refresh_token";
        campos["refresh_token"] = refreshToken;
        return SolicitarTokenAsync(credenciais, campos, cancelamento);
    }

    private async Task<TokenBling> SolicitarTokenAsync(
        CredenciaisBling aplicativo, Dictionary<string, string> campos, CancellationToken cancelamento)
    {
        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(aplicativo.ClientId + ":" + aplicativo.ClientSecret));

        string metodo = HttpMethod.Post.Method;
        string endereco = _opcoes.TokenUrl;

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, endereco);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciais);
        requisicao.Content = new FormUrlEncodedContent(campos);

        using HttpResponseMessage resposta = await EnviarComTratamentoAsync(requisicao, metodo, endereco, cancelamento);
        string corpo = await resposta.Content.ReadAsStringAsync(cancelamento);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new BlingException(
                "Erro " + (int)resposta.StatusCode + " ao obter token do Bling.",
                resposta.StatusCode, corpo, metodo, endereco, null);
        }

        TokenBling? token = JsonSerializer.Deserialize<TokenBling>(corpo, OpcoesJson);
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new BlingException("Resposta de token inválida.", resposta.StatusCode, corpo, metodo, endereco, null);
        }
        return token;
    }

    // ---------------------------------------------------------------
    // Contatos
    // ---------------------------------------------------------------

    public async Task<ContatoBling?> BuscarContatoPorDocumentoAsync(
        string conta, string accessToken, string documento, CancellationToken cancelamento)
    {
        string url = "contatos?criterio=1&numeroDocumento=" + Uri.EscapeDataString(documento);
        RespostaLista<ContatoBling>? resposta =
            await EnviarAsync<RespostaLista<ContatoBling>>(conta, accessToken, HttpMethod.Get, url, null, cancelamento);

        if (resposta == null || resposta.Data.Count == 0)
        {
            return null;
        }
        return resposta.Data[0];
    }

    public async Task<long> CriarContatoAsync(
        string conta, string accessToken, NovoContatoBling contato, CancellationToken cancelamento)
    {
        RespostaItem<Referencia>? resposta =
            await EnviarAsync<RespostaItem<Referencia>>(conta, accessToken, HttpMethod.Post, "contatos", contato, cancelamento);

        if (resposta == null || resposta.Data == null || resposta.Data.Id == 0)
        {
            throw new BlingException(
                "Bling não retornou o id do contato criado.",
                HttpStatusCode.OK, null, HttpMethod.Post.Method, EnderecoCompleto("contatos"), null);
        }
        return resposta.Data.Id;
    }

    /// <summary>
    /// Contato pelo id (a lista de contas a pagar só traz o id do fornecedor).
    /// </summary>
    public async Task<ContatoBling?> ObterContatoAsync(
        string conta, string accessToken, long id, CancellationToken cancelamento)
    {
        RespostaItem<ContatoBling>? resposta = await EnviarAsync<RespostaItem<ContatoBling>>(
            conta, accessToken, HttpMethod.Get, "contatos/" + id, null, cancelamento);
        if (resposta == null)
        {
            return null;
        }
        return resposta.Data;
    }

    // ---------------------------------------------------------------
    // Contas a pagar
    // ---------------------------------------------------------------

    public async Task<long> CriarContaPagarAsync(
        string conta, string accessToken, NovaContaPagarBling contaPagar, CancellationToken cancelamento)
    {
        string corpo = await EnviarTextoAsync(conta, accessToken, HttpMethod.Post, "contas/pagar", contaPagar, cancelamento);

        // A documentação mostra { "id": 123 }; outros endpoints usam { "data": { "id": 123 } }.
        // Aceitamos os dois formatos.
        long id = LerIdDaResposta(corpo);
        if (id == 0)
        {
            throw new BlingException(
                "Bling não retornou o id da conta a pagar criada.",
                HttpStatusCode.OK, corpo, HttpMethod.Post.Method, EnderecoCompleto("contas/pagar"), null);
        }
        return id;
    }

    // Sem "situacoes[]", a lista do Bling traz só as contas em aberto: a paga sumia da sincronização
    // e o Previsio achava que tinha sido excluída. Pede todas: em aberto, paga, parcial, devolvida e cancelada
    private const string TodasAsSituacoes =
        "&situacoes[]=1&situacoes[]=2&situacoes[]=3&situacoes[]=4&situacoes[]=5";

    /// <summary>
    /// Contas a pagar com vencimento no período, em todas as situações.
    /// </summary>
    public async Task<List<ContaPagarBling>> ListarContasPagarAsync(
        string conta, string accessToken, FiltroContasPagar filtro, CancellationToken cancelamento)
    {
        var todas = new List<ContaPagarBling>();
        int pagina = 1;
        const int limite = 100;

        while (true)
        {
            string url = "contas/pagar?pagina=" + pagina
                + "&limite=" + limite
                + "&dataVencimentoInicial=" + FormatarData(filtro.VencimentoInicial)
                + "&dataVencimentoFinal=" + FormatarData(filtro.VencimentoFinal)
                + TodasAsSituacoes;

            RespostaLista<ContaPagarBling>? resposta =
                await EnviarAsync<RespostaLista<ContaPagarBling>>(conta, accessToken, HttpMethod.Get, url, null, cancelamento);

            if (resposta == null || resposta.Data.Count == 0)
            {
                break;
            }

            todas.AddRange(resposta.Data);

            if (resposta.Data.Count < limite)
            {
                break;
            }
            pagina++;
        }

        return todas;
    }

    /// <summary>
    /// Contas a receber com vencimento no período, todas as páginas. Vão os dois jeitos de filtrar
    /// por vencimento que a API do Bling usa; quem chama confere a data de novo.
    /// </summary>
    public async Task<List<ContaReceberBling>> ListarContasReceberAsync(
        string conta, string accessToken, DateTime vencimentoInicial, DateTime vencimentoFinal, CancellationToken cancelamento)
    {
        var todas = new List<ContaReceberBling>();
        int pagina = 1;
        const int limite = 100;

        while (true)
        {
            string url = "contas/receber?pagina=" + pagina
                + "&limite=" + limite
                + "&tipoFiltroData=V"
                + "&dataInicial=" + FormatarData(vencimentoInicial)
                + "&dataFinal=" + FormatarData(vencimentoFinal)
                + "&dataVencimentoInicial=" + FormatarData(vencimentoInicial)
                + "&dataVencimentoFinal=" + FormatarData(vencimentoFinal)
                + TodasAsSituacoes;

            RespostaLista<ContaReceberBling>? resposta =
                await EnviarAsync<RespostaLista<ContaReceberBling>>(conta, accessToken, HttpMethod.Get, url, null, cancelamento);

            if (resposta == null || resposta.Data.Count == 0)
            {
                break;
            }

            todas.AddRange(resposta.Data);

            if (resposta.Data.Count < limite)
            {
                break;
            }
            pagina++;
        }

        return todas;
    }

    /// <summary>
    /// Todas as categorias de receitas e despesas da conta Bling (todas as páginas).
    /// </summary>
    public async Task<List<CategoriaBling>> ListarCategoriasAsync(
        string conta, string accessToken, CancellationToken cancelamento)
    {
        var todas = new List<CategoriaBling>();
        int pagina = 1;
        const int limite = 100;

        while (true)
        {
            string url = "categorias/receitas-despesas?pagina=" + pagina + "&limite=" + limite;
            RespostaLista<CategoriaBling>? resposta =
                await EnviarAsync<RespostaLista<CategoriaBling>>(conta, accessToken, HttpMethod.Get, url, null, cancelamento);
            if (resposta == null || resposta.Data.Count == 0)
            {
                break;
            }
            todas.AddRange(resposta.Data);
            if (resposta.Data.Count < limite)
            {
                break;
            }
            pagina++;
        }
        return todas;
    }

    /// <summary>
    /// Todas as formas de pagamento da conta Bling (todas as páginas).
    /// </summary>
    public async Task<List<FormaPagamentoBling>> ListarFormasPagamentoAsync(
        string conta, string accessToken, CancellationToken cancelamento)
    {
        var todas = new List<FormaPagamentoBling>();
        int pagina = 1;
        const int limite = 100;

        while (true)
        {
            string url = "formas-pagamentos?pagina=" + pagina + "&limite=" + limite;
            RespostaLista<FormaPagamentoBling>? resposta =
                await EnviarAsync<RespostaLista<FormaPagamentoBling>>(conta, accessToken, HttpMethod.Get, url, null, cancelamento);
            if (resposta == null || resposta.Data.Count == 0)
            {
                break;
            }
            todas.AddRange(resposta.Data);
            if (resposta.Data.Count < limite)
            {
                break;
            }
            pagina++;
        }
        return todas;
    }

    /// <summary>
    /// Detalhe de uma conta a pagar (traz o número do documento e o histórico).
    /// </summary>
    public async Task<ContaPagarBling?> ObterContaPagarAsync(
        string conta, string accessToken, long id, CancellationToken cancelamento)
    {
        RespostaItem<ContaPagarBling>? resposta = await EnviarAsync<RespostaItem<ContaPagarBling>>(
            conta, accessToken, HttpMethod.Get, "contas/pagar/" + id, null, cancelamento);
        if (resposta == null)
        {
            return null;
        }
        return resposta.Data;
    }

    // ---------------------------------------------------------------
    // Infraestrutura
    // ---------------------------------------------------------------

    public static string FormatarData(DateTime data)
    {
        return data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private async Task<T?> EnviarAsync<T>(
        string conta, string accessToken, HttpMethod metodo, string url, object? conteudo, CancellationToken cancelamento)
    {
        string corpo = await EnviarTextoAsync(conta, accessToken, metodo, url, conteudo, cancelamento);
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return default(T);
        }
        return JsonSerializer.Deserialize<T>(corpo, OpcoesJson);
    }

    private async Task<string> EnviarTextoAsync(
        string conta, string accessToken, HttpMethod metodo, string url, object? conteudo, CancellationToken cancelamento)
    {
        const int maximoTentativas = 3;
        string endereco = EnderecoCompleto(url);

        for (int tentativa = 1; tentativa <= maximoTentativas; tentativa++)
        {
            await _limitador.AguardarVezAsync(conta, _opcoes.IntervaloMinimoEntreRequisicoesMs, cancelamento);

            using var requisicao = new HttpRequestMessage(metodo, url);
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (conteudo != null)
            {
                string json = JsonSerializer.Serialize(conteudo, OpcoesJson);
                requisicao.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage resposta = await EnviarComTratamentoAsync(requisicao, metodo.Method, endereco, cancelamento);
            string corpo = await resposta.Content.ReadAsStringAsync(cancelamento);

            if (resposta.IsSuccessStatusCode)
            {
                return corpo;
            }

            if (resposta.StatusCode == HttpStatusCode.TooManyRequests && tentativa < maximoTentativas)
            {
                _logger.LogWarning("Limite de requisições do Bling atingido na conta {Conta}. Aguardando.", conta);
                await Task.Delay(1500 * tentativa, cancelamento);
                continue;
            }

            throw new BlingException(
                "Erro " + (int)resposta.StatusCode + " ao chamar " + metodo + " " + url,
                resposta.StatusCode,
                corpo,
                metodo.Method,
                endereco,
                null);
        }

        throw new BlingException(
            "Limite de tentativas excedido em " + url,
            HttpStatusCode.TooManyRequests, null, metodo.Method, endereco, null);
    }

    /// <summary>
    /// Envia a requisição e transforma falhas de rede (sem resposta do Bling)
    /// em BlingException com o endereço chamado.
    /// </summary>
    private async Task<HttpResponseMessage> EnviarComTratamentoAsync(
        HttpRequestMessage requisicao, string metodo, string endereco, CancellationToken cancelamento)
    {
        try
        {
            return await _http.SendAsync(requisicao, cancelamento);
        }
        catch (HttpRequestException erro)
        {
            throw new BlingException(
                "Sem conexão com o Bling: " + erro.Message,
                BlingException.SemResposta, null, metodo, endereco, erro);
        }
        catch (TaskCanceledException erro)
        {
            // Cancelamento pedido por quem chamou (ex.: aplicação parando) segue como cancelamento
            if (cancelamento.IsCancellationRequested)
            {
                throw;
            }
            throw new BlingException(
                "O Bling não respondeu dentro do tempo limite.",
                BlingException.SemResposta, null, metodo, endereco, erro);
        }
    }

    private string EnderecoCompleto(string urlRelativa)
    {
        if (_http.BaseAddress == null)
        {
            return urlRelativa;
        }
        return new Uri(_http.BaseAddress, urlRelativa).ToString();
    }

    private static long LerIdDaResposta(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return 0;
        }

        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;

        JsonElement data;
        if (raiz.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Object)
        {
            raiz = data;
        }

        JsonElement id;
        if (raiz.TryGetProperty("id", out id) && id.ValueKind == JsonValueKind.Number)
        {
            return id.GetInt64();
        }
        return 0;
    }
}
