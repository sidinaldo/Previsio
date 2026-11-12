namespace Previsio.Api.Dtos;

/// <summary>
/// Caixa de e-mail (da rede ou própria da empresa): quando abriu por último e o último erro.
/// </summary>
public class SaudeCaixaDto
{
    public string Usuario { get; set; } = "";
    public DateTime? UltimaLeituraEm { get; set; }
    public DateTime? UltimaFalhaEm { get; set; }
    public string? UltimoErro { get; set; }
    // A falha é mais recente que a última leitura com sucesso
    public bool Falhando { get; set; }
}
