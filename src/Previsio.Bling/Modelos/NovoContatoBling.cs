using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class NovoContatoBling
{
    [JsonPropertyName("nome")]
    public string Nome { get; set; } = "";

    [JsonPropertyName("numeroDocumento")]
    public string NumeroDocumento { get; set; } = "";

    // "J" = pessoa jurídica, "F" = pessoa física
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "J";

    // "A" = ativo
    [JsonPropertyName("situacao")]
    public string Situacao { get; set; } = "A";
}
