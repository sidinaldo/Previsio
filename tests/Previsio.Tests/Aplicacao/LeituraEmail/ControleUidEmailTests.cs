using Xunit;
using Previsio.Aplicacao.LeituraEmail;

namespace Previsio.Tests.Aplicacao.LeituraEmail;

public class ControleUidEmailTests
{
    [Fact]
    public void PrecisaRecomecar_NaPrimeiraLeitura()
    {
        Assert.True(ControleUidEmail.PrecisaRecomecar(null, null, 5));
        Assert.True(ControleUidEmail.PrecisaRecomecar(5, null, 5));
    }

    [Fact]
    public void PrecisaRecomecar_QuandoOServidorRenumerouACaixa()
    {
        Assert.True(ControleUidEmail.PrecisaRecomecar(5, 120, 6));
    }

    [Fact]
    public void NaoPrecisaRecomecar_ComAMesmaCaixa()
    {
        Assert.False(ControleUidEmail.PrecisaRecomecar(5, 120, 5));
    }

    [Fact]
    public void ProximosParaLer_IgnoraOsJaLidosEOrdenaDoMaisAntigo()
    {
        // Inclui o 120: a busca "UID 121:*" devolve o último e-mail mesmo sem nenhum novo
        var encontrados = new List<long> { 130, 120, 125 };

        List<long> proximos = ControleUidEmail.ProximosParaLer(encontrados, 120, 200);

        Assert.Equal(new long[] { 125, 130 }, proximos.ToArray());
    }

    [Fact]
    public void ProximosParaLer_RespeitaOMaximoPorExecucao()
    {
        var encontrados = new List<long> { 3, 1, 2 };

        List<long> proximos = ControleUidEmail.ProximosParaLer(encontrados, null, 2);

        Assert.Equal(new long[] { 1, 2 }, proximos.ToArray());
    }
}
