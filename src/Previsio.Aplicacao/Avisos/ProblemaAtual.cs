using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Avisos;

/// <summary>
/// Um problema encontrado agora numa rede, ainda sem saber se já tem aviso aberto.
/// </summary>
public class ProblemaAtual
{
    public TipoAviso Tipo { get; set; }
    public string Chave { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public string Link { get; set; } = "";
    public int? EmpresaId { get; set; }
    public int? NotaFiscalId { get; set; }
}
