using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.Vencimentos;

/// <summary>
/// Forma de pagamento de cada conta do e-mail de vencimentos (nome no Bling e se sai sozinha da conta).
/// </summary>
public class FormasDasContas
{
    private readonly Dictionary<string, FormaPagamento> _porChave = new Dictionary<string, FormaPagamento>();
    private readonly List<string> _gruposSaiSozinho;

    public FormasDasContas(IEnumerable<FormaPagamento> formas, List<string> gruposSaiSozinho)
    {
        foreach (FormaPagamento forma in formas)
        {
            _porChave[forma.EmpresaId + ":" + forma.BlingId] = forma;
        }
        _gruposSaiSozinho = gruposSaiSozinho;
    }

    /// <summary>O nome da forma no Bling; "Sem forma" ou "Forma não identificada" quando não há.</summary>
    public string Nome(ContaPagarSincronizada conta)
    {
        if (!conta.FormaPagamentoBlingId.HasValue)
        {
            return GruposFormaPagamento.Nome(GruposFormaPagamento.Sem);
        }
        FormaPagamento? forma;
        if (_porChave.TryGetValue(conta.EmpresaId + ":" + conta.FormaPagamentoBlingId.Value, out forma))
        {
            return forma.Descricao;
        }
        return "Forma não identificada";
    }

    public bool SaiSozinho(ContaPagarSincronizada conta)
    {
        if (!conta.FormaPagamentoBlingId.HasValue)
        {
            return false;
        }
        FormaPagamento? forma;
        if (_porChave.TryGetValue(conta.EmpresaId + ":" + conta.FormaPagamentoBlingId.Value, out forma))
        {
            return _gruposSaiSozinho.Contains(forma.Grupo);
        }
        return false;
    }
}
