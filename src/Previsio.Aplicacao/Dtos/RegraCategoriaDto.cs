namespace Previsio.Aplicacao.Dtos;

public class RegraCategoriaDto
{
    public int Id { get; set; }
    public string CnpjFornecedor { get; set; } = "";
    public string? FornecedorNome { get; set; }
    public string Categoria { get; set; } = "";
    // Empresas cujo Bling não tem essa categoria: as notas delas vão sem categoria
    public List<string> AusenteEm { get; set; } = new List<string>();
}
