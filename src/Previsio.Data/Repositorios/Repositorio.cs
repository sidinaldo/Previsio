using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;

namespace Previsio.Data.Repositorios;

/// <summary>
/// Repositório genérico sobre o EF Core.
/// </summary>
public class Repositorio<T> : IRepositorio<T> where T : class
{
    private readonly PrevisioDbContext _db;

    public Repositorio(PrevisioDbContext db)
    {
        _db = db;
    }

    public IQueryable<T> Consultar()
    {
        return _db.Set<T>();
    }

    public IQueryable<T> ConsultarSomenteLeitura()
    {
        return _db.Set<T>().AsNoTracking();
    }

    public async Task<T?> ObterPorIdAsync(int id, CancellationToken cancelamento)
    {
        return await _db.Set<T>().FindAsync(new object[] { id }, cancelamento);
    }

    public Task<bool> ExisteAsync(Expression<Func<T, bool>> condicao, CancellationToken cancelamento)
    {
        return _db.Set<T>().AnyAsync(condicao, cancelamento);
    }

    public void Adicionar(T entidade)
    {
        _db.Set<T>().Add(entidade);
    }

    public void Remover(T entidade)
    {
        _db.Set<T>().Remove(entidade);
    }

    public void Acompanhar(T entidade)
    {
        if (_db.Entry(entidade).State == EntityState.Detached)
        {
            _db.Set<T>().Attach(entidade);
        }
    }

    public Task SalvarAsync(CancellationToken cancelamento)
    {
        return _db.SaveChangesAsync(cancelamento);
    }

    public void DescartarAlteracoes()
    {
        _db.ChangeTracker.Clear();
    }
}
