namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// E-mail lido da caixa, sem depender da biblioteca de e-mail.
/// </summary>
public class EmailRecebido
{
    public long Uid { get; set; }
    public string MensagemId { get; set; } = "";

    // Remetente como o e-mail mostra ("Nome <endereco>"), para a origem da nota
    public string De { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "";
    public string Assunto { get; set; } = "";

    // Texto do e-mail (ou o HTML sem as tags, quando não há texto)
    public string Corpo { get; set; } = "";
    public DateTime? EnviadoEm { get; set; }

    public List<ArquivoDoEmail> Arquivos { get; set; } = new List<ArquivoDoEmail>();
}
