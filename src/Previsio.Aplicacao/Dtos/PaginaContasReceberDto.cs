namespace Previsio.Aplicacao.Dtos;

public class PaginaContasReceberDto
{
    public List<ContaReceberDto> Itens { get; set; } = new List<ContaReceberDto>();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanhoPagina { get; set; }
    // Soma do valor de todas as contas do filtro (não só da página)
    public decimal ValorTotal { get; set; }
    // Sincronização mais antiga entre as empresas do filtro
    public DateTime? SincronizadaEm { get; set; }
    // Janela que a sincronização traz do Bling
    public int DiasPassados { get; set; }
    public int DiasFuturos { get; set; }
    // Desligado na rede: a sincronização não traz contas a receber
    public bool FluxoCaixaAtivo { get; set; }
}
