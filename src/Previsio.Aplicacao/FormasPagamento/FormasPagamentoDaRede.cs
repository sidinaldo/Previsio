using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.FormasPagamento;

/// <summary>
/// As formas de pagamento ativas do Bling de cada empresa conectada da rede, para comparar pelo nome.
/// </summary>
public class FormasPagamentoDaRede
{
    private readonly List<Empresa> _empresas;
    private readonly List<FormaPagamento> _formas;

    public FormasPagamentoDaRede(List<Empresa> empresas, List<FormaPagamento> formas)
    {
        _empresas = empresas;
        _formas = formas;
    }

    /// <summary>Uma forma por nome (sem diferença de acento e maiúsculas), em ordem alfabética.</summary>
    public List<FormaPagamento> Opcoes()
    {
        var porNome = new Dictionary<string, FormaPagamento>();
        foreach (FormaPagamento forma in _formas)
        {
            string chave = GruposFormaPagamento.Normalizar(forma.Descricao);
            if (chave != "" && !porNome.ContainsKey(chave))
            {
                porNome[chave] = forma;
            }
        }
        return porNome.Values.OrderBy(f => f.Descricao.Trim()).ToList();
    }

    /// <summary>O nome como está no Bling (de alguma empresa), ou null se nenhuma tem.</summary>
    public string? NomeExistente(string nome)
    {
        string chave = GruposFormaPagamento.Normalizar(nome);
        foreach (FormaPagamento forma in _formas)
        {
            if (GruposFormaPagamento.Normalizar(forma.Descricao) == chave)
            {
                return forma.Descricao.Trim();
            }
        }
        return null;
    }

    /// <summary>Empresas que têm formas guardadas, mas não esta.</summary>
    public List<string> AusenteEm(string nome)
    {
        string chave = GruposFormaPagamento.Normalizar(nome);
        var ausente = new List<string>();
        foreach (Empresa empresa in _empresas)
        {
            List<FormaPagamento> daEmpresa = _formas.Where(f => f.EmpresaId == empresa.Id).ToList();
            if (daEmpresa.Count == 0)
            {
                continue;
            }
            if (!daEmpresa.Any(f => GruposFormaPagamento.Normalizar(f.Descricao) == chave))
            {
                ausente.Add(empresa.Nome);
            }
        }
        return ausente;
    }

    /// <summary>Empresas conectadas sem nenhuma forma guardada.</summary>
    public List<string> EmpresasSemFormas()
    {
        var sem = new List<string>();
        foreach (Empresa empresa in _empresas)
        {
            if (!_formas.Any(f => f.EmpresaId == empresa.Id))
            {
                sem.Add(empresa.Nome);
            }
        }
        return sem;
    }
}
