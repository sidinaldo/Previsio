using Previsio.Core.Entidades;

namespace Previsio.Core.Conferencia;

/// <summary>
/// Como reconhecer que a conta do Bling é a parcela da nota (configurado por rede).
/// </summary>
public class RegrasConferencia
{
    public bool ExigeNumeroNota { get; set; }
    public decimal ToleranciaValor { get; set; } = 0.10m;
    public int ToleranciaDias { get; set; }

    public static RegrasConferencia Da(Cliente cliente)
    {
        var regras = new RegrasConferencia();
        regras.ExigeNumeroNota = cliente.ConferenciaExigeNumeroNota;
        regras.ToleranciaValor = cliente.ConferenciaToleranciaValor;
        regras.ToleranciaDias = cliente.ConferenciaToleranciaDias;
        return regras;
    }
}
