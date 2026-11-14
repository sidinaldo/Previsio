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

    // Distribuição por NSU: último NSU devolvido nesta chamada e o maior que existe para o CNPJ.
    // Quando são iguais, não há mais nada; a próxima consulta só depois de 1 hora (senão 656)
    public string UltimoNsu { get; set; } = "";
    public string MaximoNsu { get; set; } = "";

    public bool NenhumDocumento
    {
        get { return Status == "137"; }
    }

    public bool TemMais
    {
        get { return UltimoNsu != "" && MaximoNsu != "" && string.CompareOrdinal(UltimoNsu, MaximoNsu) < 0; }
    }

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
