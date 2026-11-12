namespace Previsio.Core.Conferencia;

public class ResultadoConferencia
{
    public int Esperadas { get; set; }
    public List<ParcelaEncontrada> Encontradas { get; set; } = new List<ParcelaEncontrada>();

    public bool Nenhuma
    {
        get { return Encontradas.Count == 0; }
    }

    public bool Completa
    {
        get { return Esperadas > 0 && Encontradas.Count == Esperadas; }
    }

    public bool Parcial
    {
        get { return !Nenhuma && !Completa; }
    }

    public string Contas()
    {
        return string.Join(", ", Encontradas.Select(e => e.ContaId.ToString()));
    }
}
