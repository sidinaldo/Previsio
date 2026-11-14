namespace Previsio.Aplicacao.Dtos;

public class SemanaProjecaoDto
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public decimal Entradas { get; set; }
    public decimal Saidas { get; set; }
    public decimal Saldo { get; set; }
    public decimal SaldoAcumulado { get; set; }
    // Pico de desembolso: saídas a partir de FatorSemanaCritica vezes a média semanal
    public bool Critica { get; set; }
    public List<string> Motivos { get; set; } = new List<string>();
    // A primeira semana com saldo acumulado negativo
    public bool SaldoFicaNegativo { get; set; }
}
