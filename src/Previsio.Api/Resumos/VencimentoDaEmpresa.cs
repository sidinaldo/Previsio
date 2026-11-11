namespace Previsio.Api.Resumos;

/// <summary>
/// Contas a pagar de uma empresa num dia do resumo semanal.
/// </summary>
public class VencimentoDaEmpresa
{
    public string Empresa { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Valor { get; set; }
}
