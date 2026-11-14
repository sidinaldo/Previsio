namespace Previsio.Aplicacao.Dtos;

public class RegraFormaPagamentoDto
{
    public int Id { get; set; }
    public string CnpjFornecedor { get; set; } = "";
    public string? FornecedorNome { get; set; }
    public string Forma { get; set; } = "";
    // Empresas cujo Bling não tem essa forma: as notas delas seguem para o histórico, o XML e a padrão
    public List<string> AusenteEm { get; set; } = new List<string>();
}
