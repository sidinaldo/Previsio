using System.Linq.Expressions;

namespace Previsio.Data.Repositorios;

/// <summary>
/// Operações básicas de leitura e gravação de uma entidade.
/// Filtros, ordenação e includes ficam em quem consulta, a partir de Consultar().
/// Todos os repositórios de uma requisição compartilham o mesmo PrevisioDbContext:
/// SalvarAsync grava tudo o que foi alterado na requisição de uma vez.
/// </summary>
public interface IRepositorio<T> where T : class
{
    /// <summary>
    /// Consulta com rastreamento: use quando for alterar o que vier dela.
    /// </summary>
    IQueryable<T> Consultar();

    /// <summary>
    /// Consulta sem rastreamento: use para listagens e telas só de leitura.
    /// </summary>
    IQueryable<T> ConsultarSomenteLeitura();

    Task<T?> ObterPorIdAsync(int id, CancellationToken cancelamento);

    Task<bool> ExisteAsync(Expression<Func<T, bool>> condicao, CancellationToken cancelamento);

    void Adicionar(T entidade);

    void Remover(T entidade);

    Task SalvarAsync(CancellationToken cancelamento);

    /// <summary>
    /// Esquece as alterações ainda não gravadas (por exemplo, depois de um erro ao salvar).
    /// </summary>
    void DescartarAlteracoes();
}
