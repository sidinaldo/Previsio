namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Falha ao conectar ou entrar na caixa IMAP, já classificada pela implementação (MailKit, na Infra).
/// </summary>
public class FalhaImapException : Exception
{
    public MotivoFalhaImap Motivo { get; }

    public FalhaImapException(MotivoFalhaImap motivo, string mensagem, Exception? interna)
        : base(mensagem, interna)
    {
        Motivo = motivo;
    }
}
