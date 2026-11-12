using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Infra.Email;

/// <summary>
/// Abre sessões de leitura IMAP com o MailKit.
/// </summary>
public class LeitorImapMailKit : ILeitorImap
{
    public ISessaoImap NovaSessao()
    {
        return new SessaoImapMailKit();
    }
}
