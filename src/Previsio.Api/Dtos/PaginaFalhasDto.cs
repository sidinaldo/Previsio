namespace Previsio.Api.Dtos;

public class PaginaFalhasDto
{
    public List<FalhaDto> Itens { get; set; } = new List<FalhaDto>();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
}
