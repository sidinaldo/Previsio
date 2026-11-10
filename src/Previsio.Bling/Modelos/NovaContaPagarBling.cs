using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class NovaContaPagarBling
{
    [JsonPropertyName("vencimento")]
    public string Vencimento { get; set; } = "";

    [JsonPropertyName("valor")]
    public decimal Valor { get; set; }

    [JsonPropertyName("contato")]
    public Referencia Contato { get; set; } = new Referencia();

    [JsonPropertyName("dataEmissao")]
    public string DataEmissao { get; set; } = "";

    [JsonPropertyName("competencia")]
    public string Competencia { get; set; } = "";

    [JsonPropertyName("numeroDocumento")]
    public string NumeroDocumento { get; set; } = "";

    [JsonPropertyName("historico")]
    public string Historico { get; set; } = "";
}
