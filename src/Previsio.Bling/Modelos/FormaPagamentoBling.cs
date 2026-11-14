using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

/// <summary>
/// Forma de pagamento do Bling (GET /formas-pagamentos). "tipoPagamento" é o código tPag da NF-e
/// (15 = boleto, 17 = Pix...); situação 1 = ativa. Cada conta Bling tem as suas, com ids próprios.
/// </summary>
public class FormaPagamentoBling
{
    public const int SituacaoAtiva = 1;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = "";

    [JsonPropertyName("tipoPagamento")]
    public int TipoPagamento { get; set; }

    [JsonPropertyName("situacao")]
    public int Situacao { get; set; }
}
