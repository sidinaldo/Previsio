using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

public class ContaPagarBling
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("situacao")]
    public int Situacao { get; set; }

    [JsonPropertyName("vencimento")]
    public string Vencimento { get; set; } = "";

    [JsonPropertyName("valor")]
    public decimal Valor { get; set; }

    [JsonPropertyName("contato")]
    public Referencia? Contato { get; set; }

    // Só no detalhe da conta (GET /contas/pagar/{id}); na lista podem vir vazios
    [JsonPropertyName("numeroDocumento")]
    public string? NumeroDocumento { get; set; }

    [JsonPropertyName("historico")]
    public string? Historico { get; set; }
}
