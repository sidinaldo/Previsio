namespace Previsio.Api.Dtos;

/// <summary>
/// Marcação da nota como resolvida e lançada manualmente no Bling.
/// </summary>
public class ResolucaoManualDto
{
    // Opcional, até 500 caracteres (ex.: "lançada como conta 1234 no Bling")
    public string? Observacao { get; set; }
}
