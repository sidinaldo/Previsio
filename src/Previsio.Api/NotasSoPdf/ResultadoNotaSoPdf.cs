using Previsio.Core.Entidades;

namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// O que aconteceu com os PDFs de um e-mail sem XML.
/// </summary>
public class ResultadoNotaSoPdf
{
    // Pendências criadas
    public int Registradas { get; set; }

    // Situação da primeira pendência criada (na fila da SEFAZ ou sem chave)
    public SituacaoNotaSoPdf? Situacao { get; set; }

    // A nota (ou o mesmo e-mail) já estava no Previsio: nada a registrar, mas não foi ignorado
    public bool JaConhecido { get; set; }

    // Preenchido quando o e-mail foi ignorado: por que não virou pendência
    public string? MotivoIgnorado { get; set; }

    // Primeiro PDF utilizável do e-mail ignorado (para o admin conferir ou tratar como nota)
    public PdfDoEmail? Pdf { get; set; }
}
