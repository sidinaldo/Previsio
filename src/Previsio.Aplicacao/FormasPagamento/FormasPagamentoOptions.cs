using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.FormasPagamento;

/// <summary>
/// Seção "FormasPagamento" do appsettings.
/// </summary>
public class FormasPagamentoOptions
{
    public const string Secao = "FormasPagamento";
    public const string PadraoAutomaticoPadrao = "Boleto";

    // Forma usada quando a rede não escolheu uma padrão (pelo nome; vale se existir no Bling da empresa).
    // Vazio desliga: a conta vai sem forma de pagamento
    public string? PadraoAutomatico { get; set; } = PadraoAutomaticoPadrao;

    // Trechos do nome (sem acento e maiúsculas) que fazem a forma do Bling contar como débito automático
    public string[] TermosDebitoAutomatico { get; set; } = { "debito automatico", "debito em conta" };

    // Grupos que saem sozinhos da conta (não precisam de alguém pagar). Grupos: GruposFormaPagamento
    public string[] GruposSaiSozinho { get; set; } = { GruposFormaPagamento.DebitoAutomatico };

    /// <summary>O nome do padrão automático, ou null se estiver desligado.</summary>
    public string? PadraoAutomaticoValido()
    {
        if (string.IsNullOrWhiteSpace(PadraoAutomatico))
        {
            return null;
        }
        return PadraoAutomatico.Trim();
    }

    public List<string> TermosDebitoAutomaticoValidos()
    {
        var termos = new List<string>();
        if (TermosDebitoAutomatico == null)
        {
            return termos;
        }
        foreach (string termo in TermosDebitoAutomatico)
        {
            if (!string.IsNullOrWhiteSpace(termo))
            {
                termos.Add(termo.Trim());
            }
        }
        return termos;
    }

    /// <summary>Os grupos que saem sozinhos, só os que existem; sem nenhum válido, débito automático.</summary>
    public List<string> GruposSaiSozinhoValidos()
    {
        var grupos = new List<string>();
        if (GruposSaiSozinho != null)
        {
            foreach (string grupo in GruposSaiSozinho)
            {
                string limpo = (grupo ?? "").Trim().ToLowerInvariant();
                if (GruposFormaPagamento.Valido(limpo) && limpo != GruposFormaPagamento.Sem && !grupos.Contains(limpo))
                {
                    grupos.Add(limpo);
                }
            }
        }
        if (grupos.Count == 0)
        {
            grupos.Add(GruposFormaPagamento.DebitoAutomatico);
        }
        return grupos;
    }
}
