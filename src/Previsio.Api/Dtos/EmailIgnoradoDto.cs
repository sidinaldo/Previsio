namespace Previsio.Api.Dtos;

/// <summary>
/// E-mail com anexo que não virou nota (tela Saúde).
/// </summary>
public class EmailIgnoradoDto
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    // "Caixa da rede" ou o nome da empresa dona da caixa
    public string Caixa { get; set; } = "";
    public string Remetente { get; set; } = "";
    public string Assunto { get; set; } = "";
    public DateTime? EnviadoEm { get; set; }
    public DateTime LidoEm { get; set; }
    public string Anexos { get; set; } = "";
    public string Motivo { get; set; } = "";
    public bool TemPdf { get; set; }

    // A nota do PDF já estava no Previsio: número para o link "Ver a nota"
    public int? NotaFiscalId { get; set; }
    public string? NumeroNota { get; set; }

    // Tem PDF, a nota ainda não existe e ninguém tratou
    public bool PodeTratar { get; set; }
    public DateTime? TratadoEm { get; set; }
    public string? TratadoPor { get; set; }
    public string? ResultadoTratamento { get; set; }
}
