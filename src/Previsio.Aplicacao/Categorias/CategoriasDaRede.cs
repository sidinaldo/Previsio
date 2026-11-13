using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Categorias;

/// <summary>
/// As categorias de despesa do Bling de cada empresa conectada da rede, para comparar pelo nome.
/// </summary>
public class CategoriasDaRede
{
    private readonly List<Empresa> _empresas;
    private readonly List<CategoriaFinanceira> _categorias;

    public CategoriasDaRede(List<Empresa> empresas, List<CategoriaFinanceira> categorias)
    {
        _empresas = empresas;
        _categorias = categorias;
    }

    /// <summary>Nomes distintos (sem diferença de acento e maiúsculas), em ordem alfabética.</summary>
    public List<string> Opcoes()
    {
        var porNome = new Dictionary<string, string>();
        foreach (CategoriaFinanceira categoria in _categorias)
        {
            string chave = RegrasCategoria.Normalizar(categoria.Descricao);
            if (chave != "" && !porNome.ContainsKey(chave))
            {
                porNome[chave] = categoria.Descricao.Trim();
            }
        }
        return porNome.Values.OrderBy(n => n).ToList();
    }

    /// <summary>O nome como está no Bling (de alguma empresa), ou null se nenhuma tem.</summary>
    public string? NomeExistente(string nome)
    {
        string chave = RegrasCategoria.Normalizar(nome);
        foreach (CategoriaFinanceira categoria in _categorias)
        {
            if (RegrasCategoria.Normalizar(categoria.Descricao) == chave)
            {
                return categoria.Descricao.Trim();
            }
        }
        return null;
    }

    /// <summary>Empresas que têm categorias guardadas, mas não esta.</summary>
    public List<string> AusenteEm(string nome)
    {
        string chave = RegrasCategoria.Normalizar(nome);
        var ausente = new List<string>();
        foreach (Empresa empresa in _empresas)
        {
            List<CategoriaFinanceira> daEmpresa = _categorias.Where(k => k.EmpresaId == empresa.Id).ToList();
            if (daEmpresa.Count == 0)
            {
                continue;
            }
            if (!daEmpresa.Any(k => RegrasCategoria.Normalizar(k.Descricao) == chave))
            {
                ausente.Add(empresa.Nome);
            }
        }
        return ausente;
    }

    /// <summary>Empresas conectadas sem nenhuma categoria guardada.</summary>
    public List<string> EmpresasSemCategorias()
    {
        var sem = new List<string>();
        foreach (Empresa empresa in _empresas)
        {
            if (!_categorias.Any(k => k.EmpresaId == empresa.Id))
            {
                sem.Add(empresa.Nome);
            }
        }
        return sem;
    }
}
