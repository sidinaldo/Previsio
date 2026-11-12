namespace Previsio.Core.Entidades;

/// <summary>
/// Algo que precisa de ação numa rede. É criado quando o problema aparece e marcado
/// como resolvido sozinho quando ele some (nota lançada, Bling reconectado, caixa voltou).
/// A Chave identifica o problema ("nota:12", "bling:3", "caixa-rede:1") para não repetir aviso.
/// </summary>
public class Aviso
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public TipoAviso Tipo { get; set; }
    public string Chave { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Mensagem { get; set; } = "";

    // Tela do painel onde se resolve (ex.: "/pages/notas-pendentes")
    public string Link { get; set; } = "";

    public int? EmpresaId { get; set; }
    public int? NotaFiscalId { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvidoEm { get; set; }

    // Já foi no e-mail de avisos (nulo até o próximo envio)
    public DateTime? EmailEnviadoEm { get; set; }
}
