using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class RespostaLista<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new List<T>();
}
