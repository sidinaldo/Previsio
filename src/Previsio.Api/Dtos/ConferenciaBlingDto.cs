namespace Previsio.Api.Dtos;

/// <summary>
/// Conferência com o Bling da rede. ToleranciaValor: R$ 0,00 a R$ 5,00. ToleranciaDias: 0 a 5.
/// </summary>
public class ConferenciaBlingDto
{
    public bool ConferirAntesDeLancar { get; set; }
    public bool ExigeNumeroNota { get; set; }
    public decimal ToleranciaValor { get; set; }
    public int ToleranciaDias { get; set; }
    public bool FecharPendentes { get; set; }
}
