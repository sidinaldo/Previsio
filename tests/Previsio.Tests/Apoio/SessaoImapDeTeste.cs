using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Caixa IMAP em memória: devolve os e-mails cadastrados e registra o que foi pedido.
/// </summary>
public class SessaoImapDeTeste : ISessaoImap, ILeitorImap
{
    public Dictionary<long, EmailRecebido> Emails { get; } = new Dictionary<long, EmailRecebido>();
    public long Validade { get; set; } = 7;
    public long? Proximo { get; set; }
    public Exception? ErroAoEntrar { get; set; }

    // UIDs que dão erro ao abrir; com DerrubarNoErro, a conexão cai junto
    public HashSet<long> UidsComErro { get; } = new HashSet<long>();
    public bool DerrubarNoErro { get; set; }

    public List<string> Pedidos { get; } = new List<string>();
    public bool Conectado { get; private set; }

    public ISessaoImap NovaSessao()
    {
        return this;
    }

    public long ValidadeUids
    {
        get { return Validade; }
    }

    public long? ProximoUid
    {
        get { return Proximo; }
    }

    public Task ConectarAsync(string host, int porta, bool ssl, CancellationToken cancelamento)
    {
        Pedidos.Add("conectar " + host + ":" + porta);
        Conectado = true;
        return Task.CompletedTask;
    }

    public Task EntrarAsync(string usuario, string senha, CancellationToken cancelamento)
    {
        Pedidos.Add("entrar " + usuario + " " + senha);
        if (ErroAoEntrar != null)
        {
            throw ErroAoEntrar;
        }
        return Task.CompletedTask;
    }

    public Task AbrirCaixaDeEntradaAsync(CancellationToken cancelamento)
    {
        Pedidos.Add("abrir");
        return Task.CompletedTask;
    }

    public Task<List<long>> ProcurarRecebidosDesdeAsync(DateTime desde, CancellationToken cancelamento)
    {
        Pedidos.Add("desde");
        return Task.FromResult(Emails.Keys.ToList());
    }

    public Task<List<long>> ProcurarDepoisDoUidAsync(long ultimoUid, CancellationToken cancelamento)
    {
        Pedidos.Add("depois " + ultimoUid);
        // Como o servidor real: "UID n+1:*" devolve o último e-mail mesmo sem nenhum novo
        List<long> lista = Emails.Keys.Where(u => u > ultimoUid).ToList();
        if (lista.Count == 0 && Emails.Count > 0)
        {
            lista.Add(Emails.Keys.Max());
        }
        return Task.FromResult(lista);
    }

    public Task<EmailRecebido> ObterAsync(long uid, CancellationToken cancelamento)
    {
        Pedidos.Add("obter " + uid);
        if (UidsComErro.Contains(uid))
        {
            if (DerrubarNoErro)
            {
                Conectado = false;
            }
            throw new IOException("falha simulada no e-mail " + uid);
        }
        return Task.FromResult(Emails[uid]);
    }

    public Task DesconectarAsync(CancellationToken cancelamento)
    {
        Pedidos.Add("desconectar");
        Conectado = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
