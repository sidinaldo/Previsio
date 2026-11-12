namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Uma conexão com a caixa de entrada, somente leitura (o Previsio não marca nem altera e-mails).
/// Os passos são separados para a falha dizer exatamente onde parou.
/// </summary>
public interface ISessaoImap : IDisposable
{
    bool Conectado { get; }

    // Número que muda quando o servidor renumera a caixa (os UIDs antigos deixam de valer)
    long ValidadeUids { get; }

    // UID que o próximo e-mail vai receber, se o servidor informar
    long? ProximoUid { get; }

    Task ConectarAsync(string host, int porta, bool ssl, CancellationToken cancelamento);

    Task EntrarAsync(string usuario, string senha, CancellationToken cancelamento);

    Task AbrirCaixaDeEntradaAsync(CancellationToken cancelamento);

    Task<List<long>> ProcurarRecebidosDesdeAsync(DateTime desde, CancellationToken cancelamento);

    Task<List<long>> ProcurarDepoisDoUidAsync(long ultimoUid, CancellationToken cancelamento);

    Task<EmailRecebido> ObterAsync(long uid, CancellationToken cancelamento);

    Task DesconectarAsync(CancellationToken cancelamento);
}
