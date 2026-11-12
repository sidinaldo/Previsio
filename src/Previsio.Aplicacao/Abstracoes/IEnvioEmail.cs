namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Envia e-mails do Previsio. Lança exceção quando o servidor recusa ou não está configurado.
/// </summary>
public interface IEnvioEmail
{
    bool Configurado { get; }

    Task EnviarAsync(MensagemEmail mensagem, CancellationToken cancelamento);
}
