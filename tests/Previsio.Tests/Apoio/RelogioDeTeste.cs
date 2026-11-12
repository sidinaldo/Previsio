namespace Previsio.Tests.Apoio;

/// <summary>
/// Relógio parado num instante que o teste controla (para conferir a validade do link).
/// </summary>
public class RelogioDeTeste : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = new DateTimeOffset(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        return Agora;
    }
}
