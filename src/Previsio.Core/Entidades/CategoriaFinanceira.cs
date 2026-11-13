namespace Previsio.Core.Entidades;

/// <summary>
/// Categoria de receitas e despesas do Bling de uma empresa, guardada para o lançamento
/// (achar a categoria pelo nome) e para o painel (nome da categoria de cada conta).
/// Cada conta Bling tem as suas categorias, com ids próprios.
/// </summary>
public class CategoriaFinanceira
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa? Empresa { get; set; }

    public long BlingId { get; set; }
    // Categoria-pai no Bling (0 = raiz)
    public long BlingIdPai { get; set; }
    public string Descricao { get; set; } = "";
    // 2 = receita; as outras servem para despesa
    public int Tipo { get; set; }

    public DateTime AtualizadaEm { get; set; } = DateTime.UtcNow;
}
