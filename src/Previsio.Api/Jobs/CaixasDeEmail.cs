using Previsio.Core.Entidades;

namespace Previsio.Api.Jobs;

/// <summary>
/// Quais caixas de e-mail ler de uma rede: a da rede, se configurada, e a própria de cada
/// empresa ativa que tiver uma. A mesma caixa (servidor + usuário) é lida uma vez só.
/// </summary>
public static class CaixasDeEmail
{
    public static List<CaixaParaLer> Montar(Cliente rede)
    {
        var caixas = new List<CaixaParaLer>();
        var jaIncluidas = new HashSet<string>();

        if (Configurada(rede))
        {
            caixas.Add(new CaixaParaLer(rede, null));
            jaIncluidas.Add(Chave(rede));
        }

        foreach (Empresa empresa in rede.Empresas.OrderBy(e => e.Id))
        {
            if (!empresa.Ativo || !empresa.TemEmailProprio)
            {
                continue;
            }

            string chave = Chave(empresa);
            if (jaIncluidas.Contains(chave))
            {
                continue;
            }

            caixas.Add(new CaixaParaLer(rede, empresa));
            jaIncluidas.Add(chave);
        }

        return caixas;
    }

    private static bool Configurada(ICaixaEmail caixa)
    {
        return !string.IsNullOrWhiteSpace(caixa.ImapHost) && !string.IsNullOrWhiteSpace(caixa.ImapUsuario);
    }

    private static string Chave(ICaixaEmail caixa)
    {
        return caixa.ImapHost.Trim().ToLowerInvariant() + "|" + caixa.ImapUsuario.Trim().ToLowerInvariant();
    }
}
