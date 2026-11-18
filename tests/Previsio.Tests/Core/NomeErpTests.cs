using Previsio.Core.Entidades;
using Xunit;

namespace Previsio.Tests.Core;

public class NomeErpTests
{
    [Fact]
    public void Bling_TemONomeDele()
    {
        Assert.Equal("Bling", NomeErp.De(TipoErp.Bling));
    }

    [Fact]
    public void RedeSemEmpresa_UsaOErpDeUmaEmpresaNova()
    {
        Assert.Equal("Bling", NomeErp.DaRede(new List<TipoErp>()));
    }

    [Fact]
    public void RedeComUmSoErp_UsaONomeDele()
    {
        Assert.Equal("Bling", NomeErp.DaRede(new List<TipoErp> { TipoErp.Bling, TipoErp.Bling }));
    }

    [Fact]
    public void RedeComErpsDiferentes_UsaONomeGenerico()
    {
        Assert.Equal(NomeErp.Generico, NomeErp.DaRede(new List<TipoErp> { TipoErp.Bling, (TipoErp)99 }));
    }
}
