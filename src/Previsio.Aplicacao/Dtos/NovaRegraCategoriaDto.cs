namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// "Notas deste fornecedor vão para esta categoria". Se já houver regra para o fornecedor, troca a categoria.
/// </summary>
public class NovaRegraCategoriaDto
{
    public string CnpjFornecedor { get; set; } = "";
    public string Categoria { get; set; } = "";
}
