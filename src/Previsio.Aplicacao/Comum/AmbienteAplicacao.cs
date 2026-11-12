namespace Previsio.Aplicacao.Comum;

/// <summary>
/// O que a Aplicacao precisa saber do ambiente em que roda (o Program registra a partir do host).
/// </summary>
public class AmbienteAplicacao
{
    // Ambiente de desenvolvimento: por exemplo, sem SMTP o link de redefinição de senha vai para o log
    public bool Desenvolvimento { get; }

    public AmbienteAplicacao(bool desenvolvimento)
    {
        Desenvolvimento = desenvolvimento;
    }
}
