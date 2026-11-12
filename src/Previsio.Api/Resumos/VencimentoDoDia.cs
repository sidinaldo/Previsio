namespace Previsio.Api.Resumos;

/// <summary>
/// Um dia com contas a pagar nos próximos 7 dias, separado por empresa.
/// </summary>
public class VencimentoDoDia
{
    public DateTime Data { get; set; }
    public decimal Total { get; set; }
    public List<VencimentoDaEmpresa> Empresas { get; set; } = new List<VencimentoDaEmpresa>();
}
