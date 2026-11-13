namespace Previsio.SondagemBling;

/// <summary>
/// Um endereço da API do Bling a consultar e o que queremos descobrir com ele.
/// </summary>
public class Recurso
{
    public string Titulo { get; set; } = "";
    public string Url { get; set; } = "";
    public string Pergunta { get; set; } = "";

    public Recurso(string titulo, string url, string pergunta)
    {
        Titulo = titulo;
        Url = url;
        Pergunta = pergunta;
    }
}
