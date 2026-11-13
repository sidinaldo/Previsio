using System.Text.Json.Serialization;

namespace Previsio.Bling.Modelos;

/// <summary>
/// Categoria de receitas e despesas do Bling (GET /categorias/receitas-despesas).
/// Tipo 2 é receita; as outras servem para despesa. Cada conta Bling tem as suas, com ids próprios.
/// </summary>
public class CategoriaBling
{
    public const int TipoReceita = 2;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("idCategoriaPai")]
    public long IdCategoriaPai { get; set; }

    [JsonPropertyName("descricao")]
    public string Descricao { get; set; } = "";

    [JsonPropertyName("tipo")]
    public int Tipo { get; set; }
}
