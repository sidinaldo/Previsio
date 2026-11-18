namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// Categoria de receitas e despesas do ERP. IdPai = 0 na categoria raiz. Tipo usa a numeração
/// do Bling (a que a entidade CategoriaFinanceira guarda); outro ERP converte para ela.
/// </summary>
public class CategoriaErp
{
    public long Id { get; set; }
    public long IdPai { get; set; }
    public string Descricao { get; set; } = "";
    public int Tipo { get; set; }
}
