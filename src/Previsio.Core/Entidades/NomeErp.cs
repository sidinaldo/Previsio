namespace Previsio.Core.Entidades;

/// <summary>
/// Nome do ERP como aparece nas telas ("lançada no Bling", "Conectar Bling").
/// </summary>
public static class NomeErp
{
    // Rede com empresas em ERPs diferentes: as telas da rede falam do ERP em geral
    public const string Generico = "ERP";

    public static string De(TipoErp erp)
    {
        if (erp == TipoErp.Bling)
        {
            return "Bling";
        }
        return Generico;
    }

    /// <summary>
    /// Nome para as telas da rede: o ERP das empresas quando é um só; sem empresa ainda, o ERP
    /// que uma empresa nova recebe; empresas em ERPs diferentes, o nome genérico.
    /// </summary>
    public static string DaRede(IEnumerable<TipoErp> erpsDasEmpresas)
    {
        List<TipoErp> distintos = erpsDasEmpresas.Distinct().ToList();
        if (distintos.Count == 0)
        {
            return De(new Empresa().Erp);
        }
        if (distintos.Count == 1)
        {
            return De(distintos[0]);
        }
        return Generico;
    }
}
