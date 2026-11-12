namespace Previsio.Api.Exportacao;

/// <summary>
/// Uma coluna da planilha: título do cabeçalho, tipo do valor e largura (em caracteres).
/// </summary>
public class ColunaPlanilha
{
    public string Titulo { get; }
    public TipoColunaPlanilha Tipo { get; }
    public double Largura { get; }

    public ColunaPlanilha(string titulo, TipoColunaPlanilha tipo, double largura)
    {
        Titulo = titulo;
        Tipo = tipo;
        Largura = largura;
    }
}
