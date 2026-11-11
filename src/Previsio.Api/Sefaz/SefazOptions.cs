namespace Previsio.Api.Sefaz;

/// <summary>
/// Seção "Sefaz" do appsettings: ambiente e endereços do Ambiente Nacional da NF-e.
/// As notas reais só existem em produção; homologação serve para testar a comunicação.
/// </summary>
public class SefazOptions
{
    public const string Secao = "Sefaz";

    // "Producao" ou "Homologacao"
    public string Ambiente { get; set; } = "Producao";

    public string UrlDistribuicaoProducao { get; set; } = "https://www1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx";
    public string UrlDistribuicaoHomologacao { get; set; } = "https://hom1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx";
    public string UrlEventoProducao { get; set; } = "https://www.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx";
    public string UrlEventoHomologacao { get; set; } = "https://hom1.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx";

    // Confere o certificado do servidor da SEFAZ (cadeia ICP-Brasil). Desligar só para diagnóstico.
    public bool ValidarCertificadoServidor { get; set; } = true;

    public int TimeoutSegundos { get; set; } = 60;

    public bool EmProducao
    {
        get { return !string.Equals(Ambiente, "Homologacao", StringComparison.OrdinalIgnoreCase); }
    }

    // tpAmb: 1 = produção, 2 = homologação
    public string TipoAmbiente
    {
        get
        {
            if (EmProducao)
            {
                return "1";
            }
            return "2";
        }
    }

    public string UrlDistribuicao
    {
        get
        {
            if (EmProducao)
            {
                return UrlDistribuicaoProducao;
            }
            return UrlDistribuicaoHomologacao;
        }
    }

    public string UrlEvento
    {
        get
        {
            if (EmProducao)
            {
                return UrlEventoProducao;
            }
            return UrlEventoHomologacao;
        }
    }
}
