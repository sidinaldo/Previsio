using System.Security.Cryptography.X509Certificates;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Core.Sefaz;

namespace Previsio.Tests.Apoio;

/// <summary>
/// SEFAZ de mentira: responde o que o teste mandar, por CNPJ consultado, e guarda as chamadas.
/// </summary>
public class SefazDeTeste : ISefaz
{
    // Respostas da consulta por CNPJ, na ordem (a última se repete)
    public Dictionary<string, Queue<RetornoSefaz>> Consultas { get; } = new Dictionary<string, Queue<RetornoSefaz>>();
    public RetornoSefaz RespostaCiencia { get; set; } = Retorno("135", "Evento registrado e vinculado a NF-e");
    public bool FalharConexao { get; set; }

    public List<string> Chamadas { get; } = new List<string>();

    public static RetornoSefaz Retorno(string status, string motivo)
    {
        var retorno = new RetornoSefaz();
        retorno.Status = status;
        retorno.Motivo = motivo;
        return retorno;
    }

    public static RetornoSefaz Resumo()
    {
        RetornoSefaz retorno = Retorno("138", "Documento localizado");
        retorno.Documentos.Add(new DocumentoDistribuido { Schema = "resNFe_v1.01.xsd", Xml = "<resNFe/>" });
        return retorno;
    }

    public static RetornoSefaz Completo(string xml)
    {
        RetornoSefaz retorno = Retorno("138", "Documento localizado");
        retorno.Documentos.Add(new DocumentoDistribuido { Schema = "procNFe_v4.00.xsd", Xml = xml });
        return retorno;
    }

    public void Responder(string cnpj, params RetornoSefaz[] respostas)
    {
        Consultas[cnpj] = new Queue<RetornoSefaz>(respostas);
    }

    public Task<RetornoSefaz> ConsultarPorChaveAsync(
        X509Certificate2 certificado, string uf, string cnpj, string chave, CancellationToken cancelamento)
    {
        Chamadas.Add("consulta " + cnpj + " " + uf + " cert:" + CertificadoA1.Cnpj(certificado));
        if (FalharConexao)
        {
            throw new HttpRequestException("Conexão recusada");
        }
        if (!Consultas.ContainsKey(cnpj))
        {
            return Task.FromResult(Retorno("137", "Nenhum documento localizado"));
        }
        Queue<RetornoSefaz> fila = Consultas[cnpj];
        if (fila.Count > 1)
        {
            return Task.FromResult(fila.Dequeue());
        }
        return Task.FromResult(fila.Peek());
    }

    public Task<RetornoSefaz> ManifestarCienciaAsync(
        X509Certificate2 certificado, string cnpj, string chave, CancellationToken cancelamento)
    {
        Chamadas.Add("ciencia " + cnpj);
        return Task.FromResult(RespostaCiencia);
    }
}
