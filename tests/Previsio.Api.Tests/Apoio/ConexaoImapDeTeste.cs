using Previsio.Api.Servicos;

namespace Previsio.Api.Tests.Apoio;

/// <summary>
/// Conexão IMAP falsa: guarda o que recebeu e lança o erro configurado.
/// </summary>
public class ConexaoImapDeTeste : IConexaoImap
{
    public Exception? ErroParaLancar { get; set; }
    public bool EsperarCancelamento { get; set; }
    public string? SenhaRecebida { get; private set; }
    public string? HostRecebido { get; private set; }
    public int Chamadas { get; private set; }

    public async Task TestarAsync(string host, int porta, bool ssl, string usuario, string senha, CancellationToken cancelamento)
    {
        Chamadas++;
        HostRecebido = host;
        SenhaRecebida = senha;
        if (EsperarCancelamento)
        {
            await Task.Delay(Timeout.Infinite, cancelamento);
        }
        if (ErroParaLancar != null)
        {
            throw ErroParaLancar;
        }
    }
}
