using MailKit;
using Previsio.Api.Jobs;
using Xunit;

namespace Previsio.Api.Tests.Jobs;

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
        var encontrados = new List<UniqueId> { new UniqueId(130), new UniqueId(120), new UniqueId(125) };

        List<UniqueId> proximos = ControleUidEmail.ProximosParaLer(encontrados, 120, 200);

        Assert.Equal(new uint[] { 125, 130 }, proximos.Select(u => u.Id).ToArray());
    }

    [Fact]
    public void ProximosParaLer_RespeitaOMaximoPorExecucao()
    {
        var encontrados = new List<UniqueId> { new UniqueId(3), new UniqueId(1), new UniqueId(2) };

        List<UniqueId> proximos = ControleUidEmail.ProximosParaLer(encontrados, null, 2);

        Assert.Equal(new uint[] { 1, 2 }, proximos.Select(u => u.Id).ToArray());
    }
}
