namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Abre e fecha uma conexão IMAP só para conferir servidor, porta e login.
/// Lança a exceção do MailKit (ou de rede) quando algo falha.
/// </summary>
public interface IConexaoImap
{
    Task TestarAsync(string host, int porta, bool ssl, string usuario, string senha, CancellationToken cancelamento);
}
