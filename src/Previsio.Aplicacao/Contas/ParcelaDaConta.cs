namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Parcela de nota ligada a uma conta do Bling (quando a conta veio de nota lida pelo Previsio).
/// </summary>
public class ParcelaDaConta
{
    public int NotaFiscalId { get; set; }
    public string NumeroNota { get; set; } = "";
    public string NumeroParcela { get; set; } = "";
    public string Fornecedor { get; set; } = "";
}
