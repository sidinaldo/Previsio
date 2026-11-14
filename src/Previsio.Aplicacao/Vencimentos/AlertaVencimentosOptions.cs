namespace Previsio.Aplicacao.Vencimentos;

/// <summary>
/// Seção "AlertaVencimentos" do appsettings: o e-mail diário de vencimentos.
/// </summary>
public class AlertaVencimentosOptions
{
    public const string Secao = "AlertaVencimentos";
    public const int MaximoContasPorBlocoPadrao = 10;

    // Contas listadas em cada bloco do e-mail (hoje, amanhã, vencidas), de 1 a 50; o resto fica no link
    public int MaximoContasPorBloco { get; set; } = MaximoContasPorBlocoPadrao;

    public int MaximoContasPorBlocoValido()
    {
        if (MaximoContasPorBloco < 1 || MaximoContasPorBloco > 50)
        {
            return MaximoContasPorBlocoPadrao;
        }
        return MaximoContasPorBloco;
    }
}
