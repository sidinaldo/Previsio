using Previsio.Core.Pagamento;
using Xunit;

namespace Previsio.Tests.Core.Pagamento;

public class GruposFormaPagamentoTests
{
    private static readonly string[] Termos = { "debito automatico", "debito em conta" };

    [Theory]
    [InlineData(15, "Boleto", GruposFormaPagamento.Boleto)]
    [InlineData(17, "Pix", GruposFormaPagamento.Pix)]
    [InlineData(20, "Pix dinâmico", GruposFormaPagamento.Pix)]
    [InlineData(18, "TED", GruposFormaPagamento.Transferencia)]
    [InlineData(16, "Depósito", GruposFormaPagamento.Transferencia)]
    [InlineData(3, "Cartão de crédito", GruposFormaPagamento.Cartao)]
    [InlineData(1, "Dinheiro", GruposFormaPagamento.Dinheiro)]
    [InlineData(99, "Outros", GruposFormaPagamento.Outras)]
    [InlineData(99, "DÉBITO AUTOMÁTICO", GruposFormaPagamento.DebitoAutomatico)]
    [InlineData(15, "Boleto - débito em conta", GruposFormaPagamento.DebitoAutomatico)]
    public void Classifica_pelo_tipo_e_debito_automatico_pelo_nome(int tipo, string nome, string grupo)
    {
        Assert.Equal(grupo, GruposFormaPagamento.Classificar(tipo, nome, Termos));
    }

    [Fact]
    public void Sem_termos_debito_automatico_vale_o_tipo()
    {
        Assert.Equal(GruposFormaPagamento.Outras, GruposFormaPagamento.Classificar(99, "Débito automático", new string[0]));
    }
}
