using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class ContatoBling
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("numeroDocumento")]
    public string NumeroDocumento { get; set; } = "";
}
