namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// "Notas deste fornecedor vão com esta forma de pagamento". Se já houver regra para o fornecedor, troca a forma.
/// </summary>
public class NovaRegraFormaPagamentoDto
{
    public string CnpjFornecedor { get; set; } = "";
    public string Forma { get; set; } = "";
}
