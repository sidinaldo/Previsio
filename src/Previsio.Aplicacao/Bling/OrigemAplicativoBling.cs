using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Bling;

/// <summary>
/// De onde vem o aplicativo do Bling que a empresa usa: dela, da rede ou o global do Previsio.
/// </summary>
public static class OrigemAplicativoBling
{
    public const string Empresa = "Empresa";
    public const string Rede = "Rede";
    public const string Padrao = "Padrao";

    public static string Da(Empresa empresa, Cliente? rede)
    {
        if (!string.IsNullOrWhiteSpace(empresa.BlingClientId))
        {
            return Empresa;
        }
        if (rede != null && !string.IsNullOrWhiteSpace(rede.BlingClientId))
        {
            return Rede;
        }
        return Padrao;
    }
}
