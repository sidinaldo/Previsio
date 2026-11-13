using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

/// <summary>
/// Contato que vem dentro de uma conta. Na conta a receber o Bling costuma mandar o nome junto;
/// quando não manda, o nome é buscado pelo id.
/// </summary>
public class ContatoDaConta
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("nome")]
    public string? Nome { get; set; }
}
