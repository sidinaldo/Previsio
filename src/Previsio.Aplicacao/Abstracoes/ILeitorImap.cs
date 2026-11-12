namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Abre sessões de leitura de caixas IMAP (implementado na Infra com o MailKit).
/// </summary>
public interface ILeitorImap
{
    ISessaoImap NovaSessao();
}
