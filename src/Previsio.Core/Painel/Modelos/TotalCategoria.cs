namespace Previsio.Core.Painel.Modelos;

/// <summary>
/// Saídas do período de uma categoria do Bling (o card "Saídas por categoria" do painel).
/// </summary>
public class TotalCategoria
{
    public const string TipoCategoria = "categoria";
    // Soma das categorias que não couberam entre as maiores
    public const string TipoOutras = "outras";
    // Contas conferidas no Bling sem categoria
    public const string TipoSem = "sem";
    // Contas cuja categoria ainda não foi conferida no Bling (a sincronização busca aos poucos)
    public const string TipoAConferir = "conferir";

    public string Nome { get; set; } = "";
    public string Tipo { get; set; } = TipoCategoria;
    public decimal Valor { get; set; }
    public int Quantidade { get; set; }
}
