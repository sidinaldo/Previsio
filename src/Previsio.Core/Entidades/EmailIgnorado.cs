namespace Previsio.Core.Entidades;

/// <summary>
/// E-mail com anexo que a leitura examinou e não virou nota nem pendência (sem XML e com um PDF que
/// não parecia nota, ou com anexos que não são XML nem PDF). Serve para o admin ver por que um e-mail
/// não entrou e, se for o caso, tratá-lo como nota em PDF. Apagado sozinho depois de alguns dias.
/// </summary>
public class EmailIgnorado
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    // Empresa dona da caixa em que o e-mail chegou (nula: caixa da rede)
    public int? EmpresaCaixaId { get; set; }
    public Empresa? EmpresaCaixa { get; set; }

    public long Uid { get; set; }
    public string MensagemId { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string NomeRemetente { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string Corpo { get; set; } = "";
    public DateTime? EnviadoEm { get; set; }
    public DateTime LidoEm { get; set; } = DateTime.UtcNow;

    // Nomes dos anexos, separados por vírgula
    public string Anexos { get; set; } = "";
    public string Motivo { get; set; } = "";

    // Primeiro PDF do e-mail (para conferir e tratar como nota); nulo quando não havia PDF utilizável
    public string? PdfNome { get; set; }
    public byte[]? Pdf { get; set; }

    // "Tratar como nota em PDF"
    public DateTime? TratadoEm { get; set; }
    public string? TratadoPor { get; set; }
    public string? ResultadoTratamento { get; set; }
}
