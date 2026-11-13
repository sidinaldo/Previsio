namespace Previsio.Aplicacao.Categorias;

/// <summary>
/// Seção "Categorias" do appsettings.
/// </summary>
public class CategoriasOptions
{
    public const string Secao = "Categorias";
    public const string PadraoAutomaticoPadrao = "Compras de Mercadorias";

    // Categoria usada quando a rede não escolheu uma padrão (pelo nome; vale se existir no Bling da empresa).
    // Vazio desliga: a nota vai sem categoria
    public string? PadraoAutomatico { get; set; } = PadraoAutomaticoPadrao;

    /// <summary>O nome do padrão automático, ou null se estiver desligado.</summary>
    public string? PadraoAutomaticoValido()
    {
        if (string.IsNullOrWhiteSpace(PadraoAutomatico))
        {
            return null;
        }
        return PadraoAutomatico.Trim();
    }
}
