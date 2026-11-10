using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class RespostaItem<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }
}
