namespace Previsio.Aplicacao.Dtos;

public class SaudeEmpresaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public bool BlingConectado { get; set; }
    // Validade do token de acesso; o Previsio renova sozinho antes de vencer
    public DateTime? TokenValidoAte { get; set; }
    public DateTime? UltimaSincronizacaoEm { get; set; }
    // Só para empresa com caixa de e-mail própria
    public SaudeCaixaDto? Caixa { get; set; }
    public int NotasAguardando { get; set; }
    public int NotasComProblema { get; set; }
    public int AvisosAbertos { get; set; }
}
