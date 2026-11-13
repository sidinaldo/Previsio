using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Conta a receber sincronizada com a empresa já resolvida na consulta.
/// </summary>
public class LinhaContaReceber
{
    public ContaReceberSincronizada Conta { get; set; } = null!;
    public string EmpresaNome { get; set; } = "";
    public string EmpresaCnpj { get; set; } = "";
}
