using Previsio.Api.Email;

namespace Previsio.Api.Tests.Apoio;

/// <summary>
/// Guarda os e-mails "enviados" em vez de mandar; pode simular servidor fora do ar.
/// </summary>
public class EnvioEmailDeTeste : IEnvioEmail
{
    public List<MensagemEmail> Enviados { get; } = new List<MensagemEmail>();
    public bool Configurado { get; set; } = true;
    public bool Falhar { get; set; }

    public Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento)
    {
        if (Falhar)
        {
            throw new InvalidOperationException("Servidor de e-mail fora do ar");
        }
        Enviados.Add(mensagem);
        return Task.CompletedTask;
    }
}
