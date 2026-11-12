using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Bling;

namespace Previsio.Tests.Apoio;

/// <summary>
/// BlingClient apontando para o RespostasFixas, sem espera entre requisições.
/// </summary>
public static class BlingDeTeste
{
    public const string ApiBaseUrl = "https://api.bling.com.br/Api/v3/";

    public static BlingClient Criar(RespostasFixas respostas)
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
