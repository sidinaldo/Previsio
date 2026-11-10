using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class Referencia
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
