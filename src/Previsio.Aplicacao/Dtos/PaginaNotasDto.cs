namespace Previsio.Aplicacao.Dtos;

public class PaginaNotasDto
{
    public List<NotaRecebidaDto> Itens { get; set; } = new List<NotaRecebidaDto>();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
}
