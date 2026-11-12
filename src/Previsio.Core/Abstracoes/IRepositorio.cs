using System.Linq.Expressions;

namespace Previsio.Core.Abstracoes;

/// <summary>
/// Operações básicas de leitura e gravação de uma entidade (implementado em Previsio.Data).
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

    /// <summary>
    /// Volta a acompanhar uma entidade já carregada (por exemplo, depois de DescartarAlteracoes),
    /// para as mudanças nela serem gravadas no próximo SalvarAsync.
    /// </summary>
    void Acompanhar(T entidade);

    Task SalvarAsync(CancellationToken cancelamento);

    /// <summary>
    /// Esquece as alterações ainda não gravadas (por exemplo, depois de um erro ao salvar).
    /// </summary>
    void DescartarAlteracoes();
}
