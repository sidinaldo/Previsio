namespace Previsio.Aplicacao.Dtos;

public class PaginaContasPagarDto
{
    public List<ContaPagarDto> Itens { get; set; } = new List<ContaPagarDto>();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }

    // Soma do valor de todas as contas do filtro (não só da página)
    public decimal ValorTotal { get; set; }

    // Sincronização mais antiga entre as empresas do filtro (o quão atual está a lista)
    public DateTime? SincronizadaEm { get; set; }

    // Janela que a sincronização traz do Bling, para a tela explicar o que não aparece
    public int DiasPassados { get; set; }
    public int DiasFuturos { get; set; }
}
