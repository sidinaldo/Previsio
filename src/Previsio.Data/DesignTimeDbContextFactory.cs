using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Previsio.Data;

/// <summary>
/// Usado apenas pelo "dotnet ef migrations" em tempo de desenvolvimento.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PrevisioDbContext>
{
    public PrevisioDbContext CreateDbContext(string[] args)
    {
        string? conexao = Environment.GetEnvironmentVariable("PREVISIO_DB");
        if (string.IsNullOrEmpty(conexao))
        {
            conexao = "Host=localhost;Port=5432;Database=previsio;Username=previsio;Password=previsio";
        }

        var opcoes = new DbContextOptionsBuilder<PrevisioDbContext>();
        opcoes.UseNpgsql(conexao);
        return new PrevisioDbContext(opcoes.Options);
    }
}
