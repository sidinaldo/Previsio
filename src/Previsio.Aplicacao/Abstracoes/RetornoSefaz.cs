namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Resposta da SEFAZ: código de status (cStat), motivo e, na distribuição, os documentos achados.
/// </summary>
public class RetornoSefaz
{
    // 138 = documento localizado; 137 = nenhum documento; 656 = consumo indevido (esperar 1 hora)
    // Evento: 135/136 = registrado; 573 = já registrado antes
    public string Status { get; set; } = "";
    public string Motivo { get; set; } = "";
    public List<DocumentoDistribuido> Documentos { get; set; } = new List<DocumentoDistribuido>();

    public bool DocumentoLocalizado
    {
        get { return Status == "138"; }
    }

    public bool ConsumoIndevido
    {
        get { return Status == "656"; }
    }

    public bool EventoRegistrado
    {
        get { return Status == "135" || Status == "136" || Status == "573"; }
    }

    public string Descricao
    {
        get { return Status + " - " + Motivo; }
    }
}
