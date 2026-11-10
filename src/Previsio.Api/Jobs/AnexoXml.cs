namespace Previsio.Api.Jobs;

/// <summary>
/// Arquivo .xml encontrado num e-mail (direto ou dentro de um .zip).
/// </summary>
public class AnexoXml
{
    public string Nome { get; set; } = "";
    public string Conteudo { get; set; } = "";
}
