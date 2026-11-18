using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao.Abstracoes.Erp;
using Previsio.Bling;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Bling apontando para o RespostasFixas, sem espera entre requisições. Criar devolve o Bling atrás
/// da interface de ERP (o que os serviços usam); CriarCliente, o BlingClient puro.
/// </summary>
public static class BlingDeTeste
{
    public const string ApiBaseUrl = "https://api.bling.com.br/Api/v3/";

    public static IErp Criar(RespostasFixas respostas)
    {
        return new ErpBling(CriarCliente(respostas));
    }

    public static BlingClient CriarCliente(RespostasFixas respostas)
    {
        var opcoes = new BlingOptions();
        opcoes.ClientId = "id-teste";
        opcoes.ClientSecret = "segredo-teste";
        opcoes.ApiBaseUrl = ApiBaseUrl;
        opcoes.IntervaloMinimoEntreRequisicoesMs = 0;

        return new BlingClient(
            new HttpClient(respostas),
            Options.Create(opcoes),
            new LimitadorRequisicoes(),
            NullLogger<BlingClient>.Instance);
    }
}
