using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Resumos;

/// <summary>
/// Números do resumo semanal de uma rede, já calculados para o e-mail.
/// </summary>
public class ResumoSemanal
{
    public string ClienteNome { get; set; } = "";
    public DateTime Hoje { get; set; }

    // Contas a pagar em aberto no Bling: próximos 30 dias e próximos 7, dia a dia
    public decimal Total30Dias { get; set; }
    public int Contas30Dias { get; set; }
    public decimal Total7Dias { get; set; }
    public List<VencimentoDoDia> Dias { get; set; } = new List<VencimentoDoDia>();

    // Resumo de todo dia útil (e não de um dia da semana)
    public bool Diario { get; set; }

    // Notas lidas do e-mail desde o último resumo (horário de Brasília)
    public DateTime NotasDesde { get; set; }
    public int NotasRecebidas { get; set; }
    public decimal ValorNotasRecebidas { get; set; }
    public int NotasLancadas { get; set; }
    public int NotasComProblema { get; set; }

    // Contas em aberto no Bling com vencimento antes de hoje
    public int Vencidas { get; set; }
    public decimal TotalVencidas { get; set; }

    // Os mesmos avisos abertos do sininho
    public List<Aviso> Problemas { get; set; } = new List<Aviso>();
}
