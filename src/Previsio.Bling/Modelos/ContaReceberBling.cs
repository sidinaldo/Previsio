using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

/// <summary>
/// Conta a receber na lista do Bling (GET /contas/receber). Situação usa os mesmos códigos
/// da conta a pagar: 1 em aberto, 2 recebida, 3 parcialmente recebida, 4 devolvida, 5 cancelada.
/// </summary>
public class ContaReceberBling
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
    public ContatoDaConta? Contato { get; set; }
}
