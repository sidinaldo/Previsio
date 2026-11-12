namespace Previsio.Api.Exportacao;

public class ArquivoExportado
{
    public const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string Nome { get; set; } = "";
    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
}
