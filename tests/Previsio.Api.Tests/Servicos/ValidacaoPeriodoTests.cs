using Previsio.Api.Servicos;
using Xunit;

namespace Previsio.Api.Tests.Servicos;

public class ValidacaoPeriodoTests
{
    [Fact]
    public void PeriodoValido_SemMensagem()
    {
        Assert.Null(ValidacaoPeriodo.Validar(new DateTime(2026, 11, 1), new DateTime(2026, 11, 10)));
        Assert.Null(ValidacaoPeriodo.Validar(null, new DateTime(2026, 11, 10)));
        Assert.Null(ValidacaoPeriodo.Validar(new DateTime(2026, 11, 1), null));
    }

    [Fact]
    public void DataFinalAntesDaInicial_EhInvalido()
    {
        Assert.Equal("A data final deve ser igual ou posterior à inicial.",
            ValidacaoPeriodo.Validar(new DateTime(2026, 11, 10), new DateTime(2026, 11, 1)));
    }

    [Fact]
    public void PeriodoMaiorQueUmAno_EhInvalido()
    {
        Assert.Equal("O período pode ter no máximo um ano.",
            ValidacaoPeriodo.Validar(new DateTime(2025, 1, 1), new DateTime(2026, 11, 10)));
    }
}
