namespace Previsio.Aplicacao.EmailsIgnorados;

/// <summary>
/// Dados do e-mail ignorado tirados da mensagem na leitura.
/// </summary>
public class EmailParaRegistrar
{
    public int ClienteId { get; set; }
    public int? EmpresaCaixaId { get; set; }
    public long Uid { get; set; }
    public string MensagemId { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string Corpo { get; set; } = "";
    public DateTime? EnviadoEm { get; set; }
    public List<string> Anexos { get; set; } = new List<string>();
    public string Motivo { get; set; } = "";
    public string? PdfNome { get; set; }
    public byte[]? Pdf { get; set; }
    public int? NotaFiscalId { get; set; }
    public string? NumeroNota { get; set; }
}
