namespace Previsio.Aplicacao.Dtos;

public class SaudeJobDto
{
    public string Id { get; set; } = "";
    public string Nome { get; set; } = "";
    // Ok, Falhou, TentandoDeNovo, Rodando, NuncaRodou
    public string Situacao { get; set; } = "";
    public DateTime? UltimaExecucaoEm { get; set; }
    public DateTime? ProximaExecucaoEm { get; set; }
    public string? Erro { get; set; }
}
