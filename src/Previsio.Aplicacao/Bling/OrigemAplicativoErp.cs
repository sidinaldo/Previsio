using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Bling;

/// <summary>
/// De onde vem o aplicativo do Bling que a empresa usa: dela, da rede ou o global do Previsio.
/// </summary>
public static class OrigemAplicativoErp
{
    public const string Empresa = "Empresa";
    public const string Rede = "Rede";
    public const string Padrao = "Padrao";

    public static string Da(Empresa empresa, Cliente? rede)
    {
        if (!string.IsNullOrWhiteSpace(empresa.ErpClientId))
        {
            return Empresa;
        }
        if (rede != null && !string.IsNullOrWhiteSpace(rede.ErpClientId))
        {
            return Rede;
        }
        return Padrao;
    }
}
