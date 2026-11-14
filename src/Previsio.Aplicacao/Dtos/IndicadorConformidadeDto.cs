namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Um indicador da auditoria: quanto está em ordem (percentual) e o que falta arrumar (quantidade e valor).
/// </summary>
public class IndicadorConformidadeDto
{
    // "categorias", "notas" ou "em-dia"
    public string Chave { get; set; } = "";
    public string Nome { get; set; } = "";
    // 0 a 100; nulo quando não há base para calcular (nenhuma conta ou nota no período)
    public int? Percentual { get; set; }
    public string Faixa { get; set; } = "";
    public int Peso { get; set; }
    // O que falta arrumar
    public int Quantidade { get; set; }
    public decimal Valor { get; set; }
    public string Descricao { get; set; } = "";
    // Quantos ainda não foram conferidos (só categorias)
    public int QuantidadeAConferir { get; set; }
}
