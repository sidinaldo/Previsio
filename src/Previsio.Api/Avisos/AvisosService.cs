using System.Net;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Email;
using Previsio.Api.Jobs;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Core.Contratos;

namespace Previsio.Api.Avisos;

/// <summary>
/// Avisos do que precisa de alguém: nota que o Previsio não consegue lançar, empresa sem Bling
/// e caixa de e-mail que não abre. Olha o estado atual de cada rede, abre aviso para problema
/// novo e fecha sozinho o que já foi resolvido. O e-mail junta os avisos novos de uma vez.
/// </summary>
public class AvisosService
{
    // Notas que o Previsio não resolve sozinho. "Erro" só depois de todas as tentativas de lançamento;
    // "Bling desconectado" vira um aviso só, da empresa, e não um por nota.
    private static readonly StatusNota[] StatusComAviso = new StatusNota[]
    {
        StatusNota.SemParcelas,
        StatusNota.EmpresaNaoEncontrada,
        StatusNota.Erro
    };

    // Os dois jobs e a tela podem atualizar ao mesmo tempo: um de cada vez neste processo
    private static readonly SemaphoreSlim UmPorVez = new SemaphoreSlim(1, 1);

    public const int MaximoItensNoEmail = 20;

    private readonly PrevisioDbContext _db;
    private readonly IEnvioEmail _email;
    private readonly IConfiguration _configuracao;
    private readonly TimeProvider _relogio;
    private readonly ILogger<AvisosService> _logger;

    public AvisosService(
        PrevisioDbContext db, IEnvioEmail email, IConfiguration configuracao, TimeProvider relogio, ILogger<AvisosService> logger)
    {
        _db = db;
        _email = email;
        _configuracao = configuracao;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>
    /// Confere as redes ativas (ou só a informada): abre avisos novos e fecha os resolvidos.
    /// </summary>
    public async Task AtualizarAsync(int? clienteId, CancellationToken cancelamento)
    {
        await UmPorVez.WaitAsync(cancelamento);
        try
        {
            IQueryable<Cliente> consulta = _db.Clientes.Include(c => c.Empresas).Where(c => c.Ativo);
            if (clienteId.HasValue)
            {
                consulta = consulta.Where(c => c.Id == clienteId.Value);
            }
            List<Cliente> clientes = await consulta.ToListAsync(cancelamento);

            DateTime agora = _relogio.GetUtcNow().UtcDateTime;
            foreach (Cliente cliente in clientes)
            {
                List<ProblemaAtual> problemas = await ProblemasDaRedeAsync(cliente, cancelamento);
                await SincronizarAsync(cliente.Id, problemas, agora, cancelamento);
            }
        }
        finally
        {
            UmPorVez.Release();
        }
    }

    /// <summary>
    /// Envia, a cada usuário, um e-mail com os avisos novos que ele pode ver
    /// (admin: de todas as redes; usuário de cliente: só da rede dele).
    /// </summary>
    public async Task EnviarNovosAsync(CancellationToken cancelamento)
    {
        List<Aviso> novos = await _db.Avisos
            .Include(a => a.Cliente)
            .Where(a => a.ResolvidoEm == null && a.EmailEnviadoEm == null)
            .OrderBy(a => a.ClienteId)
            .ThenBy(a => a.CriadoEm)
            .ToListAsync(cancelamento);
        if (novos.Count == 0)
        {
            return;
        }

        if (!_email.Configurado)
        {
            _logger.LogWarning("Smtp não configurado: {Quantidade} avisos novos ficam só no sininho", novos.Count);
        }
        else
        {
            List<Usuario> usuarios = await _db.Usuarios
                .Include(u => u.Cliente)
                .Where(u => u.Ativo)
                .ToListAsync(cancelamento);

            foreach (Usuario usuario in usuarios)
            {
                List<Aviso> visiveis = VisiveisPara(usuario, novos);
                if (visiveis.Count == 0)
                {
                    continue;
                }
                try
                {
                    await _email.EnviarAsync(MontarEmail(usuario, visiveis), cancelamento);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception erro)
                {
                    // Um destinatário com problema não impede os outros
                    _logger.LogError(erro, "Falha ao enviar o e-mail de avisos ao usuário {UsuarioId}", usuario.Id);
                }
            }
        }

        // Marca mesmo sem SMTP ou com falha: o aviso continua no sininho e não vira uma enxurrada depois
        DateTime agora = _relogio.GetUtcNow().UtcDateTime;
        foreach (Aviso aviso in novos)
        {
            aviso.EmailEnviadoEm = agora;
        }
        await _db.SaveChangesAsync(cancelamento);
    }

    /// <summary>
    /// Avisos abertos que o usuário pode ver, do mais novo para o mais antigo.
    /// </summary>
    public async Task<List<Aviso>> ListarAbertosAsync(Usuario usuario, int limite, CancellationToken cancelamento)
    {
        IQueryable<Aviso> consulta = _db.Avisos
            .AsNoTracking()
            .Include(a => a.Cliente)
            .Where(a => a.ResolvidoEm == null && a.Cliente!.Ativo);
        if (usuario.Perfil != PerfilUsuario.Admin)
        {
            int clienteId = usuario.ClienteId ?? 0;
            consulta = consulta.Where(a => a.ClienteId == clienteId);
        }
        return await consulta
            .OrderByDescending(a => a.CriadoEm)
            .ThenByDescending(a => a.Id)
            .Take(limite)
            .ToListAsync(cancelamento);
    }

    public const int LimiteSininho = 30;

    /// <summary>
    /// O sininho do usuário: atualiza os avisos da rede dele (ou de todas, para o admin)
    /// e devolve os abertos, marcando os criados depois da última vez que ele abriu.
    /// </summary>
    public async Task<PainelAvisosDto?> PainelAsync(int usuarioId, CancellationToken cancelamento)
    {
        Usuario? usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
        if (usuario == null)
        {
            return null;
        }

        if (usuario.Perfil == PerfilUsuario.Admin)
        {
            await AtualizarAsync(null, cancelamento);
        }
        else if (usuario.ClienteId.HasValue)
        {
            await AtualizarAsync(usuario.ClienteId.Value, cancelamento);
        }

        List<Aviso> abertos = await ListarAbertosAsync(usuario, int.MaxValue, cancelamento);
        var painel = new PainelAvisosDto();
        painel.Total = abertos.Count;
        foreach (Aviso aviso in abertos)
        {
            bool novo = usuario.AvisosVistosEm == null || aviso.CriadoEm > usuario.AvisosVistosEm.Value;
            if (novo)
            {
                painel.Novos++;
            }
            if (painel.Itens.Count < LimiteSininho)
            {
                painel.Itens.Add(ParaDto(aviso, novo));
            }
        }
        return painel;
    }

    /// <summary>
    /// O usuário abriu o sininho: o número volta a zero até chegar aviso novo.
    /// </summary>
    public async Task<bool> MarcarVistosAsync(int usuarioId, CancellationToken cancelamento)
    {
        Usuario? usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
        if (usuario == null)
        {
            return false;
        }
        usuario.AvisosVistosEm = _relogio.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancelamento);
        return true;
    }

    private static AvisoDto ParaDto(Aviso aviso, bool novo)
    {
        var dto = new AvisoDto();
        dto.Id = aviso.Id;
        dto.Tipo = aviso.Tipo.ToString();
        dto.Titulo = aviso.Titulo;
        dto.Mensagem = aviso.Mensagem;
        dto.Link = aviso.Link;
        dto.ClienteId = aviso.ClienteId;
        if (aviso.Cliente != null)
        {
            dto.ClienteNome = aviso.Cliente.Nome;
        }
        dto.CriadoEm = aviso.CriadoEm;
        dto.Novo = novo;
        return dto;
    }

    private static List<Aviso> VisiveisPara(Usuario usuario, List<Aviso> avisos)
    {
        if (usuario.Perfil == PerfilUsuario.Admin)
        {
            return avisos;
        }
        if (usuario.ClienteId == null || usuario.Cliente == null || !usuario.Cliente.Ativo)
        {
            return new List<Aviso>();
        }
        int clienteId = usuario.ClienteId.Value;
        return avisos.Where(a => a.ClienteId == clienteId).ToList();
    }

    private async Task<List<ProblemaAtual>> ProblemasDaRedeAsync(Cliente cliente, CancellationToken cancelamento)
    {
        var problemas = new List<ProblemaAtual>();

        List<NotaFiscal> notas = await _db.NotasFiscais
            .AsNoTracking()
            .Include(n => n.Empresa)
            .Where(n => n.ClienteId == cliente.Id && StatusComAviso.Contains(n.Status))
            .ToListAsync(cancelamento);
        foreach (NotaFiscal nota in notas)
        {
            if (nota.Status == StatusNota.Erro && nota.Tentativas < LancamentoNotaJob.MaximoTentativas)
            {
                // Ainda vai ser tentada de novo sozinha
                continue;
            }
            problemas.Add(ProblemaDaNota(nota));
        }

        // Notas que chegaram só em PDF e precisam de alguém (a busca automática na SEFAZ não resolveu)
        List<NotaSoPdf> soPdf = await _db.NotasSoPdf
            .AsNoTracking()
            .Where(n => n.ClienteId == cliente.Id
                && (n.Situacao == SituacaoNotaSoPdf.SemChave
                    || n.Situacao == SituacaoNotaSoPdf.SemCertificado
                    || n.Situacao == SituacaoNotaSoPdf.NaoResolvida))
            .Select(n => new NotaSoPdf { Id = n.Id, ChaveAcesso = n.ChaveAcesso, Assunto = n.Assunto, Remetente = n.Remetente, NomeRemetente = n.NomeRemetente, Motivo = n.Motivo, EmpresaCaixaId = n.EmpresaCaixaId })
            .ToListAsync(cancelamento);
        foreach (NotaSoPdf nota in soPdf)
        {
            problemas.Add(ProblemaDaNotaSoPdf(nota));
        }

        foreach (Empresa empresa in cliente.Empresas)
        {
            if (!empresa.Ativo)
            {
                continue;
            }
            if (!empresa.BlingConectado)
            {
                var problema = new ProblemaAtual();
                problema.Tipo = TipoAviso.BlingDesconectado;
                problema.Chave = "bling:" + empresa.Id;
                problema.Titulo = "Bling desconectado: " + empresa.Nome;
                problema.Mensagem = "As notas desta empresa não estão indo para o Bling. Clique em \"Conectar Bling\" em Empresas.";
                problema.Link = "/pages/empresas";
                problema.EmpresaId = empresa.Id;
                problemas.Add(problema);
            }
            if (empresa.TemEmailProprio && CaixaFalhando(empresa))
            {
                problemas.Add(ProblemaDaCaixa("caixa-empresa:" + empresa.Id, empresa, empresa.Id));
            }
        }

        problemas.AddRange(await ContasVencidasAsync(cliente, cancelamento));

        if (cliente.ImapHost.Trim() != "" && CaixaFalhando(cliente))
        {
            problemas.Add(ProblemaDaCaixa("caixa-rede:" + cliente.Id, cliente, null));
        }

        return problemas;
    }

    /// <summary>
    /// Um aviso por loja com contas em aberto no Bling vencidas há pelo menos a tolerância da rede.
    /// Some sozinho quando as contas são pagas (ou baixadas) no Bling e a sincronização traz isso.
    /// </summary>
    private async Task<List<ProblemaAtual>> ContasVencidasAsync(Cliente cliente, CancellationToken cancelamento)
    {
        var problemas = new List<ProblemaAtual>();
        List<Empresa> empresas = cliente.Empresas.Where(e => e.Ativo).ToList();
        if (empresas.Count == 0)
        {
            return problemas;
        }
        DateTime hoje = PeriodoTeste.HojeBrasil(_relogio.GetUtcNow().UtcDateTime);
        List<int> ids = empresas.Select(e => e.Id).ToList();
        List<ContaPagarSincronizada> vencidas = await Vencimentos.ContasEmAberto
            .Vencidas(_db, ids, hoje, cliente.AvisoVencidasDias)
            .ToListAsync(cancelamento);

        foreach (var grupo in vencidas.GroupBy(c => c.EmpresaId))
        {
            Empresa empresa = empresas.First(e => e.Id == grupo.Key);
            int quantidade = grupo.Count();
            decimal total = grupo.Sum(c => c.Valor);
            DateTime maisAntiga = grupo.Min(c => c.Vencimento);

            var problema = new ProblemaAtual();
            problema.Tipo = TipoAviso.ContasVencidas;
            problema.Chave = "vencidas:" + empresa.Id;
            if (quantidade == 1)
            {
                problema.Titulo = Limitar("1 conta vencida: " + empresa.Nome, 200);
            }
            else
            {
                problema.Titulo = Limitar(quantidade + " contas vencidas: " + empresa.Nome, 200);
            }
            problema.Mensagem = Limitar(Resumos.ModeloEmailResumo.Dinheiro(total) + " em aberto no Bling; a mais antiga venceu em "
                + maisAntiga.ToString("dd/MM/yyyy") + ". Pague ou dê baixa no Bling.", 500);
            problema.Link = "/pages/painel";
            problema.EmpresaId = empresa.Id;
            problemas.Add(problema);
        }
        return problemas;
    }

    private static bool CaixaFalhando(ICaixaEmail caixa)
    {
        if (caixa.ImapUltimaFalhaConexaoEm == null)
        {
            return false;
        }
        if (caixa.ImapUltimaConexaoEm == null)
        {
            return true;
        }
        return caixa.ImapUltimaFalhaConexaoEm.Value > caixa.ImapUltimaConexaoEm.Value;
    }

    private static ProblemaAtual ProblemaDaCaixa(string chave, ICaixaEmail caixa, int? empresaId)
    {
        var problema = new ProblemaAtual();
        problema.Tipo = TipoAviso.CaixaEmailFalhando;
        problema.Chave = chave;
        problema.Titulo = "Caixa de e-mail com erro: " + caixa.ImapUsuario;
        problema.Mensagem = Limitar(
            "O Previsio não consegue ler as notas desta caixa. " + (caixa.ImapUltimoErroConexao ?? ""), 500);
        problema.Link = "/pages/falhas";
        problema.EmpresaId = empresaId;
        return problema;
    }

    private static ProblemaAtual ProblemaDaNotaSoPdf(NotaSoPdf nota)
    {
        string fornecedor = nota.NomeRemetente != "" ? nota.NomeRemetente : nota.Remetente;
        string titulo = "Nota só em PDF";
        if (nota.ChaveAcesso != null && nota.ChaveAcesso.Length == 44)
        {
            titulo = titulo + ": NF " + Previsio.Core.Sefaz.ChaveAcesso.Numero(nota.ChaveAcesso);
        }
        if (fornecedor != "")
        {
            titulo = titulo + " de " + fornecedor;
        }
        var problema = new ProblemaAtual();
        problema.Tipo = TipoAviso.NotaComProblema;
        problema.Chave = "pdf:" + nota.Id;
        problema.Titulo = Limitar(titulo, 200);
        problema.Mensagem = Limitar(nota.Motivo ?? "Chegou o PDF sem o XML. Anexe o XML ou peça ao fornecedor.", 500);
        problema.Link = "/pages/notas-pendentes";
        problema.EmpresaId = nota.EmpresaCaixaId;
        return problema;
    }

    private static ProblemaAtual ProblemaDaNota(NotaFiscal nota)
    {
        string situacao;
        if (nota.Status == StatusNota.SemParcelas)
        {
            situacao = "sem vencimentos no XML";
        }
        else if (nota.Status == StatusNota.EmpresaNaoEncontrada)
        {
            situacao = "CNPJ " + FormatarCnpj(nota.CnpjDestinatario) + " não cadastrado";
        }
        else
        {
            situacao = "erro ao lançar no Bling";
        }

        string empresa = "";
        if (nota.Empresa != null)
        {
            empresa = " · " + nota.Empresa.Nome;
        }

        var problema = new ProblemaAtual();
        problema.Tipo = TipoAviso.NotaComProblema;
        problema.Chave = "nota:" + nota.Id;
        problema.Titulo = Limitar("NF " + nota.Numero + ": " + situacao, 200);
        problema.Mensagem = Limitar(nota.NomeFornecedor + empresa + ". " + (nota.MensagemErro ?? ""), 500);
        problema.Link = "/pages/notas-pendentes";
        problema.EmpresaId = nota.EmpresaId;
        problema.NotaFiscalId = nota.Id;
        return problema;
    }

    private async Task SincronizarAsync(int clienteId, List<ProblemaAtual> problemas, DateTime agora, CancellationToken cancelamento)
    {
        List<Aviso> abertos = await _db.Avisos
            .Where(a => a.ClienteId == clienteId && a.ResolvidoEm == null)
            .ToListAsync(cancelamento);

        var chavesAtuais = new HashSet<string>(problemas.Select(p => p.Chave));
        foreach (Aviso aviso in abertos)
        {
            if (!chavesAtuais.Contains(aviso.Chave))
            {
                aviso.ResolvidoEm = agora;
            }
        }

        var chavesAbertas = new HashSet<string>(abertos.Select(a => a.Chave));
        foreach (ProblemaAtual problema in problemas)
        {
            if (chavesAbertas.Contains(problema.Chave))
            {
                // O mesmo problema mudou de tamanho (ex.: mais contas vencidas): atualiza o texto, sem aviso novo
                Aviso existente = abertos.First(a => a.Chave == problema.Chave);
                existente.Titulo = problema.Titulo;
                existente.Mensagem = problema.Mensagem;
                continue;
            }
            var aviso = new Aviso();
            aviso.ClienteId = clienteId;
            aviso.Tipo = problema.Tipo;
            aviso.Chave = problema.Chave;
            aviso.Titulo = problema.Titulo;
            aviso.Mensagem = problema.Mensagem;
            aviso.Link = problema.Link;
            aviso.EmpresaId = problema.EmpresaId;
            aviso.NotaFiscalId = problema.NotaFiscalId;
            aviso.CriadoEm = agora;
            _db.Avisos.Add(aviso);
        }

        await _db.SaveChangesAsync(cancelamento);
    }

    private MensagemEmail MontarEmail(Usuario usuario, List<Aviso> avisos)
    {
        string urlFront = (_configuracao["Front:Url"] ?? "http://localhost:4200/#").TrimEnd('/');
        bool variasRedes = avisos.Select(a => a.ClienteId).Distinct().Count() > 1;

        var html = new System.Text.StringBuilder();
        var texto = new System.Text.StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#0F172A;max-width:560px\">");
        html.Append("<p style=\"font-size:20px;font-weight:600;color:#0B1B36;margin:0 0 16px\">Previsio</p>");
        html.Append("<p>Olá, " + WebUtility.HtmlEncode(usuario.Nome) + ". Isto precisa de atenção:</p>");
        html.Append("<ul style=\"padding-left:20px\">");
        texto.Append("Olá, " + usuario.Nome + ". Isto precisa de atenção:\n\n");

        int mostrados = 0;
        foreach (Aviso aviso in avisos)
        {
            if (mostrados == MaximoItensNoEmail)
            {
                break;
            }
            string rede = "";
            if (variasRedes && aviso.Cliente != null)
            {
                rede = aviso.Cliente.Nome + " · ";
            }
            string link = urlFront + aviso.Link;
            html.Append("<li style=\"margin-bottom:10px\"><a href=\"" + WebUtility.HtmlEncode(link) + "\" style=\"color:#1D4ED8;font-weight:600\">"
                + WebUtility.HtmlEncode(rede + aviso.Titulo) + "</a><br><span style=\"color:#475569\">"
                + WebUtility.HtmlEncode(aviso.Mensagem) + "</span></li>");
            texto.Append("- " + rede + aviso.Titulo + "\n  " + aviso.Mensagem + "\n  " + link + "\n");
            mostrados++;
        }
        html.Append("</ul>");
        if (avisos.Count > mostrados)
        {
            string mais = "E mais " + (avisos.Count - mostrados) + " avisos no sininho do Previsio.";
            html.Append("<p>" + mais + "</p>");
            texto.Append("\n" + mais + "\n");
        }
        html.Append("<p style=\"color:#5E6E85;font-size:12px\">Os avisos somem sozinhos quando o problema é resolvido.</p></div>");

        var mensagem = new MensagemEmail();
        mensagem.Para = usuario.Email;
        mensagem.NomePara = usuario.Nome;
        if (avisos.Count == 1)
        {
            mensagem.Assunto = "Previsio: " + avisos[0].Titulo;
        }
        else
        {
            mensagem.Assunto = "Previsio: " + avisos.Count + " avisos precisam de atenção";
        }
        mensagem.Html = html.ToString();
        mensagem.Texto = texto.ToString();
        return mensagem;
    }

    private static string FormatarCnpj(string cnpj)
    {
        if (cnpj == null || cnpj.Length != 14)
        {
            return cnpj ?? "";
        }
        return cnpj.Substring(0, 2) + "." + cnpj.Substring(2, 3) + "." + cnpj.Substring(5, 3) + "/"
            + cnpj.Substring(8, 4) + "-" + cnpj.Substring(12, 2);
    }

    private static string Limitar(string texto, int tamanho)
    {
        string limpo = texto.Trim();
        if (limpo.Length <= tamanho)
        {
            return limpo;
        }
        return limpo.Substring(0, tamanho - 1) + "…";
    }
}
