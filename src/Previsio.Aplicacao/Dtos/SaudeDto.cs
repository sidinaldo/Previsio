namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Tela Saúde (admin): os jobs e, por rede, a caixa de e-mail, o Bling das empresas e a fila de notas.
/// </summary>
public class SaudeDto
{
    public DateTime GeradoEm { get; set; }
    public int RedesComAtencao { get; set; }
    public List<SaudeJobDto> Jobs { get; set; } = new List<SaudeJobDto>();
    public List<SaudeRedeDto> Redes { get; set; } = new List<SaudeRedeDto>();
}
