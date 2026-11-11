using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Options;
using Previsio.Api.Servicos;

namespace Previsio.Api.Sefaz;

/// <summary>
/// Chamadas SOAP 1.2 ao Ambiente Nacional, com o certificado da empresa na conexão TLS (exigência da SEFAZ).
/// Cada chamada abre a própria conexão: o certificado muda de empresa para empresa.
/// </summary>
public class SefazSoap : ISefaz
{
    private readonly SefazOptions _opcoes;

    public SefazSoap(IOptions<SefazOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public async Task<RetornoSefaz> ConsultarPorChaveAsync(
        X509Certificate2 certificado, string uf, string cnpj, string chave, CancellationToken cancelamento)
    {
        string mensagem = MensagensSefaz.ConsultaPorChave(_opcoes.TipoAmbiente, UfIbge.Codigo(uf), cnpj, chave);
        string resposta = await EnviarAsync(certificado, _opcoes.UrlDistribuicao, MensagensSefaz.AcaoDistribuicao, mensagem, cancelamento);
        return MensagensSefaz.LerRetornoDistribuicao(resposta);
    }

    public async Task<RetornoSefaz> ManifestarCienciaAsync(
        X509Certificate2 certificado, string cnpj, string chave, CancellationToken cancelamento)
    {
        DateTimeOffset agora = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, RelogioBrasil.Fuso);
        // A SEFAZ recusa evento com horário à frente do dela; 1 minuto de folga para relógio adiantado
        agora = agora.AddMinutes(-1);
        string mensagem = MensagensSefaz.EventoCienciaAssinado(_opcoes.TipoAmbiente, cnpj, chave, agora, certificado);
        string resposta = await EnviarAsync(certificado, _opcoes.UrlEvento, MensagensSefaz.AcaoEvento, mensagem, cancelamento);
        return MensagensSefaz.LerRetornoEvento(resposta);
    }

    private async Task<string> EnviarAsync(
        X509Certificate2 certificado, string url, string acao, string mensagem, CancellationToken cancelamento)
    {
        using var conexao = new HttpClientHandler();
        conexao.ClientCertificateOptions = ClientCertificateOption.Manual;
        conexao.ClientCertificates.Add(certificado);
        conexao.SslProtocols = SslProtocols.Tls12;
        if (!_opcoes.ValidarCertificadoServidor)
        {
            conexao.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var cliente = new HttpClient(conexao);
        cliente.Timeout = TimeSpan.FromSeconds(_opcoes.TimeoutSegundos);

        var conteudo = new StringContent(mensagem, Encoding.UTF8);
        conteudo.Headers.ContentType = MediaTypeHeaderValue.Parse("application/soap+xml; charset=utf-8; action=\"" + acao + "\"");

        using HttpResponseMessage resposta = await cliente.PostAsync(url, conteudo, cancelamento);
        string corpo = await resposta.Content.ReadAsStringAsync(cancelamento);
        if (!resposta.IsSuccessStatusCode && !corpo.Contains("Envelope"))
        {
            throw new HttpRequestException("A SEFAZ respondeu HTTP " + (int)resposta.StatusCode + ".");
        }
        return corpo;
    }
}
