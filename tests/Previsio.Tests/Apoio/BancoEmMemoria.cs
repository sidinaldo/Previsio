using Microsoft.EntityFrameworkCore;
using Previsio.Data;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Banco EF Core em memória, um por teste (nome aleatório), para os testes não se enxergarem.
/// </summary>
public static class BancoEmMemoria
{
    public static PrevisioDbContext Criar()
    {
        DbContextOptions<PrevisioDbContext> opcoes = new DbContextOptionsBuilder<PrevisioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PrevisioDbContext(opcoes);
    }
}
