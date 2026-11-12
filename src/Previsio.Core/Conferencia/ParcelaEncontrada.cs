using Previsio.Core.Entidades;

namespace Previsio.Core.Conferencia;

/// <summary>
/// Parcela da nota e a conta do Bling que corresponde a ela. Parcela nula: nota sem parcelas (valor total).
/// </summary>
public class ParcelaEncontrada
{
    public ParcelaNota? Parcela { get; set; }
    public long ContaId { get; set; }
}
