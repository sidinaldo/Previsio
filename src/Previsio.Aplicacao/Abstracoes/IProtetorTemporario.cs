namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Assina um valor que vale por pouco tempo (o "state" do OAuth do Bling, que leva a empresa).
/// </summary>
public interface IProtetorTemporario
{
    string Proteger(string valor, TimeSpan validade);

    /// <summary>
    /// Devolve o valor original. Lança exceção se o texto foi alterado ou se passou da validade.
    /// </summary>
    string Revelar(string valorProtegido);
}
