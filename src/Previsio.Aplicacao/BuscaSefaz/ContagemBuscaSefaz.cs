namespace Previsio.Aplicacao.BuscaSefaz;

/// <summary>
/// O que uma busca de uma empresa fez, para o texto da situação na tela.
/// </summary>
public class ContagemBuscaSefaz
{
    public int NotasNovas { get; set; }
    public int Ciencias { get; set; }
    public int CienciasRecusadas { get; set; }
    public int Canceladas { get; set; }
    public int CanceladasDepoisDeLancadas { get; set; }

    public string Resumo()
    {
        var partes = new List<string>();
        if (NotasNovas > 0)
        {
            partes.Add(Plural(NotasNovas, "nota nova", "notas novas"));
        }
        if (Ciencias > 0)
        {
            partes.Add("ciência registrada em " + Plural(Ciencias, "nota", "notas") + " (o XML chega na próxima busca)");
        }
        if (Canceladas > 0)
        {
            partes.Add(Plural(Canceladas, "nota cancelada pelo emitente", "notas canceladas pelo emitente"));
        }
        if (CanceladasDepoisDeLancadas > 0)
        {
            partes.Add(Plural(CanceladasDepoisDeLancadas, "nota já lançada foi cancelada pelo emitente", "notas já lançadas foram canceladas pelo emitente")
                + ": confira no Bling");
        }
        if (CienciasRecusadas > 0)
        {
            partes.Add("a SEFAZ recusou a ciência de " + Plural(CienciasRecusadas, "nota", "notas"));
        }
        if (partes.Count == 0)
        {
            return "Nenhuma nota nova.";
        }
        string texto = string.Join("; ", partes);
        return char.ToUpper(texto[0]) + texto.Substring(1) + ".";
    }

    private static string Plural(int quantidade, string singular, string plural)
    {
        if (quantidade == 1)
        {
            return "1 " + singular;
        }
        return quantidade + " " + plural;
    }
}
