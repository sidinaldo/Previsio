using Previsio.Aplicacao.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Relatorios;

/// <summary>
/// O dossiê e o que a planilha precisa além dele (as contas do período com os nomes).
/// </summary>
public class DossieCompleto
{
    public DossieDto Dossie { get; set; } = new DossieDto();
    public List<ContaPagarSincronizada> ContasPeriodo { get; set; } = new List<ContaPagarSincronizada>();
    public Dictionary<int, string> NomesEmpresas { get; set; } = new Dictionary<int, string>();
    public Dictionary<string, string> NomesCategorias { get; set; } = new Dictionary<string, string>();
}
