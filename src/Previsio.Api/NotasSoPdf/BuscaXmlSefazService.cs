using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Sefaz;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;

namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// Busca na SEFAZ o XML das notas que chegaram só em PDF, pela chave de acesso.
/// Para cada loja da rede com certificado: consulta a chave; se a SEFAZ devolver só o resumo,
/// registra a Ciência da Operação e consulta de novo minutos depois, quando o XML completo é liberado.
/// Com o XML, a nota é registrada como se tivesse chegado por e-mail e segue para o Bling.
/// </summary>
public class BuscaXmlSefazService
{
    public const int MaximoTentativas = 6;
    public const int LotePorExecucao = 20;

    // Espera entre as tentativas (a nota pode demorar a aparecer na distribuição da SEFAZ)
    private static readonly TimeSpan[] Esperas = new TimeSpan[]
    {
        TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), TimeSpan.FromHours(24)
    };

    // Depois da ciência, o XML costuma ser liberado em poucos minutos
    private static readonly TimeSpan EsperaDepoisDaCiencia = TimeSpan.FromMinutes(5);
    // "Consumo indevido" (656): a SEFAZ bloqueia o CNPJ por 1 hora
    private static readonly TimeSpan EsperaConsumoIndevido = TimeSpan.FromMinutes(65);

    private readonly PrevisioDbContext _db;
    private readonly ISefaz _sefaz;
    private readonly CertificadosService _certificados;
    private readonly RegistroNotaService _registro;
    private readonly LeitorNFe _leitor;
    private readonly TimeProvider _relogio;
    private readonly ILogger<BuscaXmlSefazService> _logger;

    public BuscaXmlSefazService(
        PrevisioDbContext db,
        ISefaz sefaz,
        CertificadosService certificados,
        RegistroNotaService registro,
        LeitorNFe leitor,
        TimeProvider relogio,
        ILogger<BuscaXmlSefazService> logger)
    {
        _db = db;
        _sefaz = sefaz;
        _certificados = certificados;
        _registro = registro;
        _leitor = leitor;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>
    /// Processa as notas na fila da SEFAZ cuja hora de tentar chegou.
    /// </summary>
    public async Task ProcessarPendentesAsync(CancellationToken cancelamento)
    {
        DateTime agora = Agora();
        List<int> ids = await _db.NotasSoPdf
            .Where(n => n.Situacao == SituacaoNotaSoPdf.BuscandoNaSefaz
                && n.Cliente!.Ativo
                && (n.ProximaTentativaEm == null || n.ProximaTentativaEm <= agora))
            .OrderBy(n => n.ProximaTentativaEm)
            .ThenBy(n => n.Id)
            .Select(n => n.Id)
            .Take(LotePorExecucao)
            .ToListAsync(cancelamento);

        var certificados = new Dictionary<int, X509Certificate2?>();
        try
        {
            foreach (int id in ids)
            {
                try
                {
                    await ProcessarAsync(id, certificados, cancelamento);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception erro)
                {
                    _logger.LogError(erro, "Erro ao buscar na SEFAZ o XML da nota em PDF {Id}", id);
                }
            }
        }
        finally
        {
            foreach (X509Certificate2? certificado in certificados.Values)
            {
                if (certificado != null)
                {
                    certificado.Dispose();
                }
            }
        }
    }

    private async Task ProcessarAsync(int id, Dictionary<int, X509Certificate2?> certificados, CancellationToken cancelamento)
    {
        _db.ChangeTracker.Clear();
        NotaSoPdf? item = await _db.NotasSoPdf.FirstOrDefaultAsync(n => n.Id == id, cancelamento);
        if (item == null || item.ChaveAcesso == null)
        {
            return;
        }
        string chave = item.ChaveAcesso;

        // O XML pode ter chegado por outro e-mail enquanto isso
        NotaFiscal? existente = await _db.NotasFiscais.FirstOrDefaultAsync(
            n => n.ClienteId == item.ClienteId && n.ChaveAcesso == chave, cancelamento);
        if (existente != null)
        {
            Resolver(item, existente.Id, "XML recebido em outro e-mail");
            await _db.SaveChangesAsync(cancelamento);
            return;
        }

        List<Empresa> empresas = await _db.Empresas
            .Where(e => e.ClienteId == item.ClienteId && e.Ativo)
            .OrderBy(e => e.Id)
            .ToListAsync(cancelamento);
        List<ConsultaSefaz> consultas = Consultas(item, empresas);
        if (consultas.Count == 0)
        {
            item.Situacao = SituacaoNotaSoPdf.SemCertificado;
            item.Motivo = "Nenhuma loja da rede tem certificado digital A1 cadastrado (com a UF) para buscar o XML na SEFAZ. "
                + "Cadastre em Empresas ou anexe o XML.";
            await _db.SaveChangesAsync(cancelamento);
            return;
        }

        var recusas = new List<string>();
        foreach (ConsultaSefaz consulta in consultas)
        {
            X509Certificate2? certificado = Certificado(consulta.DonoDoCertificado, certificados);
            if (certificado == null)
            {
                recusas.Add(consulta.Empresa.Nome + ": o certificado de " + consulta.DonoDoCertificado.Nome + " não abre (senha ou arquivo)");
                continue;
            }

            RetornoSefaz retorno;
            try
            {
                retorno = await _sefaz.ConsultarPorChaveAsync(certificado, consulta.Uf, consulta.Empresa.Cnpj, chave, cancelamento);
            }
            catch (TaskCanceledException)
            {
                if (cancelamento.IsCancellationRequested)
                {
                    throw;
                }
                // Tempo esgotado do HttpClient (o cancelamento do job relança acima)
                await Reagendar(item, "A SEFAZ não respondeu a tempo.", true, cancelamento);
                return;
            }
            catch (HttpRequestException erro)
            {
                await Reagendar(item, "Não consegui falar com a SEFAZ: " + erro.Message, true, cancelamento);
                return;
            }

            if (retorno.ConsumoIndevido)
            {
                item.ProximaTentativaEm = Agora().Add(EsperaConsumoIndevido);
                item.Motivo = "A SEFAZ pediu para esperar 1 hora antes de consultar de novo (" + retorno.Descricao + ").";
                await _db.SaveChangesAsync(cancelamento);
                return;
            }

            if (!retorno.DocumentoLocalizado)
            {
                recusas.Add(consulta.Empresa.Nome + ": " + retorno.Descricao);
                continue;
            }

            // Achou: esta loja é a destinatária
            item.EmpresaDestinoId = consulta.Empresa.Id;
            DocumentoDistribuido? completo = retorno.Documentos.FirstOrDefault(d => d.NotaCompleta);
            if (completo != null)
            {
                await RegistrarXmlAsync(item, completo.Xml, cancelamento);
                return;
            }

            if (item.CienciaEnviadaEm == null)
            {
                await ManifestarAsync(item, certificado, consulta.Empresa, chave, cancelamento);
                return;
            }

            await Reagendar(item, "Ciência da operação registrada; aguardando a SEFAZ liberar o XML completo.", true, cancelamento);
            return;
        }

        await Reagendar(item, "A SEFAZ ainda não encontrou a nota para as lojas da rede. " + string.Join("; ", recusas), true, cancelamento);
    }

    private async Task ManifestarAsync(NotaSoPdf item, X509Certificate2 certificado, Empresa empresa, string chave, CancellationToken cancelamento)
    {
        RetornoSefaz evento;
        try
        {
            evento = await _sefaz.ManifestarCienciaAsync(certificado, empresa.Cnpj, chave, cancelamento);
        }
        catch (HttpRequestException erro)
        {
            await Reagendar(item, "Não consegui registrar a ciência da operação na SEFAZ: " + erro.Message, true, cancelamento);
            return;
        }

        if (!evento.EventoRegistrado)
        {
            await Reagendar(item, "A SEFAZ recusou a ciência da operação: " + evento.Descricao, true, cancelamento);
            return;
        }

        item.CienciaEnviadaEm = Agora();
        item.ProximaTentativaEm = Agora().Add(EsperaDepoisDaCiencia);
        item.Motivo = "Ciência da operação registrada por " + empresa.Nome + "; buscando o XML completo em alguns minutos.";
        await _db.SaveChangesAsync(cancelamento);
    }

    private async Task RegistrarXmlAsync(NotaSoPdf item, string xml, CancellationToken cancelamento)
    {
        NotaFiscalXml? nota = _leitor.Ler(xml);
        if (nota == null)
        {
            await Reagendar(item, "A SEFAZ devolveu um XML que não consegui ler.", true, cancelamento);
            return;
        }
        await _registro.RegistrarAsync(item.ClienteId, nota, item.OrigemEmail, item.EmpresaCaixaId, cancelamento);
        int? notaId = await _db.NotasFiscais
            .Where(n => n.ClienteId == item.ClienteId && n.ChaveAcesso == nota.ChaveAcesso)
            .Select(n => (int?)n.Id)
            .FirstOrDefaultAsync(cancelamento);
        Resolver(item, notaId, "XML baixado da SEFAZ");
        await _db.SaveChangesAsync(cancelamento);
    }

    /// <summary>
    /// Conta a tentativa (se for o caso) e marca a próxima; na última, a nota fica para alguém resolver.
    /// </summary>
    private async Task Reagendar(NotaSoPdf item, string motivo, bool contaTentativa, CancellationToken cancelamento)
    {
        if (contaTentativa)
        {
            item.TentativasSefaz++;
        }
        if (item.TentativasSefaz >= MaximoTentativas)
        {
            item.Situacao = SituacaoNotaSoPdf.NaoResolvida;
            item.ProximaTentativaEm = null;
            item.Motivo = Limitar("Não consegui o XML na SEFAZ depois de " + MaximoTentativas + " tentativas. " + motivo, 500);
        }
        else
        {
            int indice = Math.Min(Math.Max(item.TentativasSefaz - 1, 0), Esperas.Length - 1);
            item.ProximaTentativaEm = Agora().Add(Esperas[indice]);
            item.Motivo = Limitar(motivo, 500);
        }
        await _db.SaveChangesAsync(cancelamento);
    }

    private void Resolver(NotaSoPdf item, int? notaFiscalId, string como)
    {
        item.Situacao = SituacaoNotaSoPdf.Resolvida;
        item.NotaFiscalId = notaFiscalId;
        item.ResolvidaEm = Agora();
        item.ResolvidaPor = como;
        item.ProximaTentativaEm = null;
        item.Motivo = null;
    }

    /// <summary>
    /// Em que ordem consultar: a loja já confirmada como destinatária, a dona da caixa e as demais.
    /// Cada loja usa o próprio certificado ou o de outra loja da mesma raiz de CNPJ (a matriz).
    /// </summary>
    public static List<ConsultaSefaz> Consultas(NotaSoPdf item, List<Empresa> empresas)
    {
        var ordem = new List<Empresa>();
        AdicionarPrimeiro(ordem, empresas, item.EmpresaDestinoId);
        AdicionarPrimeiro(ordem, empresas, item.EmpresaCaixaId);
        foreach (Empresa empresa in empresas)
        {
            if (!ordem.Contains(empresa))
            {
                ordem.Add(empresa);
            }
        }

        var consultas = new List<ConsultaSefaz>();
        foreach (Empresa empresa in ordem)
        {
            Empresa? dono = null;
            if (empresa.TemCertificado)
            {
                dono = empresa;
            }
            else
            {
                string raiz = Raiz(empresa.Cnpj);
                dono = empresas.FirstOrDefault(e => e.TemCertificado && Raiz(e.Cnpj) == raiz);
            }
            if (dono == null)
            {
                continue;
            }
            string? uf = empresa.Uf;
            if (!UfIbge.Valida(uf))
            {
                uf = dono.Uf;
            }
            if (!UfIbge.Valida(uf))
            {
                continue;
            }
            var consulta = new ConsultaSefaz();
            consulta.Empresa = empresa;
            consulta.DonoDoCertificado = dono;
            consulta.Uf = uf!;
            consultas.Add(consulta);
        }
        return consultas;
    }

    private static void AdicionarPrimeiro(List<Empresa> ordem, List<Empresa> empresas, int? empresaId)
    {
        if (!empresaId.HasValue)
        {
            return;
        }
        Empresa? empresa = empresas.FirstOrDefault(e => e.Id == empresaId.Value);
        if (empresa != null && !ordem.Contains(empresa))
        {
            ordem.Add(empresa);
        }
    }

    private X509Certificate2? Certificado(Empresa dono, Dictionary<int, X509Certificate2?> certificados)
    {
        if (!certificados.ContainsKey(dono.Id))
        {
            certificados[dono.Id] = _certificados.Abrir(dono);
        }
        return certificados[dono.Id];
    }

    private static string Raiz(string cnpj)
    {
        if (cnpj.Length < 8)
        {
            return cnpj;
        }
        return cnpj.Substring(0, 8);
    }

    private DateTime Agora()
    {
        return _relogio.GetUtcNow().UtcDateTime;
    }

    private static string Limitar(string texto, int tamanho)
    {
        if (texto.Length <= tamanho)
        {
            return texto;
        }
        return texto.Substring(0, tamanho);
    }
}
