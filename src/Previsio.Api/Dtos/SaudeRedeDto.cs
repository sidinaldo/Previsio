namespace Previsio.Api.Dtos;

public class SaudeRedeDto
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = "";
    // Tem aviso aberto (os mesmos do sininho)
    public bool Atencao { get; set; }
    public int AvisosAbertos { get; set; }
    public SaudeCaixaDto? Caixa { get; set; }
    public int NotasAguardando { get; set; }
    public int NotasComProblema { get; set; }
    public DateTime? UltimaNotaEm { get; set; }
    public List<SaudeEmpresaDto> Empresas { get; set; } = new List<SaudeEmpresaDto>();
}
