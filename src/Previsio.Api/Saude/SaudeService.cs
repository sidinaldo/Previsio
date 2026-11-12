using Microsoft.EntityFrameworkCore;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Core.Contratos;

namespace Previsio.Api.Saude;

/// <summary>
/// Visão do admin sobre as integrações: os jobs agendados e, por rede, a caixa de e-mail,
/// o Bling de cada empresa e a fila de notas. Uma rede pede atenção quando tem aviso aberto.
/// </summary>
public class SaudeService
{
    // Ordem e nome dos jobs na tela; um job desconhecido aparece no fim com o próprio id
    private static readonly string[][] NomesJobs = new string[][]
    {
        new string[] { "leitura-email", "Leitura dos e-mails" },
        new string[] { "lancamento", "Lançamento no Bling" },
        new string[] { "avisos", "Avisos" },
        new string[] { "sincronizacao", "Sincronização das contas do Bling" },
        new string[] { "busca-xml-sefaz", "XML na SEFAZ (notas só em PDF)" },
        new string[] { "resumo-semanal", "Resumo por e-mail" },
        new string[] { "alerta-vencimentos", "Alerta de vencimentos (hoje e amanhã)" },
        new string[] { "avisos-teste", "Avisos do teste grátis" }
    };

    private readonly PrevisioDbContext _db;
    private readonly AvisosService _avisos;
    private readonly IAgendaJobs _agenda;
    private readonly TimeProvider _relogio;

    public SaudeService(PrevisioDbContext db, AvisosService avisos, IAgendaJobs agenda, TimeProvider relogio)
    {
        _db = db;
        _avisos = avisos;
        _agenda = agenda;
        _relogio = relogio;
    }

    public async Task<SaudeDto> ObterAsync(CancellationToken cancelamento)
    {
        // Os avisos ficam em dia antes de decidir quem pede atenção
        await _avisos.AtualizarAsync(null, cancelamento);

        var saude = new SaudeDto();
        saude.GeradoEm = _relogio.GetUtcNow().UtcDateTime;
        saude.Jobs = Jobs();

        List<Cliente> clientes = await _db.Clientes.AsNoTracking()
            .Include(c => c.Empresas)
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .ToListAsync(cancelamento);

        List<Aviso> avisos = await _db.Avisos.AsNoTracking()
            .Where(a => a.ResolvidoEm == null)
            .ToListAsync(cancelamento);

        // Contagem das notas por rede, empresa e situação, sem trazer as notas
        var contagens = await _db.NotasFiscais.AsNoTracking()
            .GroupBy(n => new { n.ClienteId, n.EmpresaId, n.Status })
            .Select(g => new { g.Key.ClienteId, g.Key.EmpresaId, g.Key.Status, Quantidade = g.Count(), UltimaEm = g.Max(n => n.RecebidaEm) })
            .ToListAsync(cancelamento);

        foreach (Cliente cliente in clientes)
        {
            var rede = new SaudeRedeDto();
            rede.ClienteId = cliente.Id;
            rede.Nome = cliente.Nome;
            rede.Contrato = PeriodoTeste.Situacao(cliente, saude.GeradoEm);
            if (cliente.Contrato == SituacaoContrato.EmTeste && cliente.TesteTerminaEm.HasValue)
            {
                rede.DiasRestantesTeste = PeriodoTeste.DiasRestantes(cliente.TesteTerminaEm.Value, saude.GeradoEm);
            }
            rede.AvisosAbertos = avisos.Count(a => a.ClienteId == cliente.Id);
            rede.Atencao = rede.AvisosAbertos > 0;
            rede.Caixa = Caixa(cliente);

            var daRede = contagens.Where(c => c.ClienteId == cliente.Id).ToList();
            rede.NotasAguardando = daRede.Where(c => c.Status == StatusNota.Recebida).Sum(c => c.Quantidade);
            rede.NotasComProblema = daRede.Where(c => StatusComProblema.Todos.Contains(c.Status)).Sum(c => c.Quantidade);
            if (daRede.Count > 0)
            {
                rede.UltimaNotaEm = daRede.Max(c => c.UltimaEm);
            }

            foreach (Empresa empresa in cliente.Empresas.Where(e => e.Ativo).OrderBy(e => e.Nome))
            {
                var item = new SaudeEmpresaDto();
                item.Id = empresa.Id;
                item.Nome = empresa.Nome;
                item.Cnpj = empresa.Cnpj;
                item.BlingConectado = empresa.BlingConectado;
                if (empresa.BlingConectado)
                {
                    item.TokenValidoAte = empresa.BlingTokenExpiraEm;
                }
                item.UltimaSincronizacaoEm = empresa.UltimaSincronizacaoEm;
                if (empresa.TemEmailProprio)
                {
                    item.Caixa = Caixa(empresa);
                }
                var daEmpresa = daRede.Where(c => c.EmpresaId == empresa.Id).ToList();
                item.NotasAguardando = daEmpresa.Where(c => c.Status == StatusNota.Recebida).Sum(c => c.Quantidade);
                item.NotasComProblema = daEmpresa.Where(c => StatusComProblema.Todos.Contains(c.Status)).Sum(c => c.Quantidade);
                item.AvisosAbertos = avisos.Count(a => a.EmpresaId == empresa.Id);
                rede.Empresas.Add(item);
            }
            saude.Redes.Add(rede);
        }

        // Quem pede atenção primeiro
        saude.Redes = saude.Redes.OrderByDescending(r => r.Atencao).ThenBy(r => r.Nome).ToList();
        saude.RedesComAtencao = saude.Redes.Count(r => r.Atencao);
        return saude;
    }

    private List<SaudeJobDto> Jobs()
    {
        List<ExecucaoAgendada> agendados = _agenda.Agendados();
        var lista = new List<SaudeJobDto>();
        foreach (string[] nome in NomesJobs)
        {
            ExecucaoAgendada? execucao = agendados.FirstOrDefault(a => a.Id == nome[0]);
            if (execucao != null)
            {
                lista.Add(Job(execucao, nome[1]));
            }
        }
        foreach (ExecucaoAgendada execucao in agendados)
        {
            bool conhecido = NomesJobs.Any(n => n[0] == execucao.Id);
            if (!conhecido)
            {
                lista.Add(Job(execucao, execucao.Id));
            }
        }
        return lista;
    }

    private static SaudeJobDto Job(ExecucaoAgendada execucao, string nome)
    {
        var job = new SaudeJobDto();
        job.Id = execucao.Id;
        job.Nome = nome;
        job.UltimaExecucaoEm = execucao.UltimaExecucaoEm;
        job.ProximaExecucaoEm = execucao.ProximaExecucaoEm;
        job.Situacao = Situacao(execucao);
        if (job.Situacao != "Ok" && job.Situacao != "Rodando")
        {
            job.Erro = execucao.Erro;
        }
        return job;
    }

    private static string Situacao(ExecucaoAgendada execucao)
    {
        string estado = execucao.UltimaSituacao ?? "";
        if (estado == "Succeeded")
        {
            return "Ok";
        }
        if (estado == "Failed")
        {
            return "Falhou";
        }
        if (estado == "Scheduled")
        {
            // Hangfire agenda uma nova tentativa depois de uma falha
            return "TentandoDeNovo";
        }
        if (estado == "Processing" || estado == "Enqueued")
        {
            return "Rodando";
        }
        if (!execucao.UltimaExecucaoEm.HasValue)
        {
            return "NuncaRodou";
        }
        // Estado que o Hangfire já apagou (expirou): a execução aconteceu e não ficou erro
        return "Ok";
    }

    private static SaudeCaixaDto Caixa(ICaixaEmail caixa)
    {
        var dto = new SaudeCaixaDto();
        dto.Usuario = caixa.ImapUsuario;
        dto.UltimaLeituraEm = caixa.ImapUltimaConexaoEm;
        dto.UltimaFalhaEm = caixa.ImapUltimaFalhaConexaoEm;
        dto.UltimoErro = caixa.ImapUltimoErroConexao;
        if (caixa.ImapUltimaFalhaConexaoEm.HasValue)
        {
            if (!caixa.ImapUltimaConexaoEm.HasValue || caixa.ImapUltimaFalhaConexaoEm.Value > caixa.ImapUltimaConexaoEm.Value)
            {
                dto.Falhando = true;
            }
        }
        if (!dto.Falhando)
        {
            dto.UltimoErro = null;
        }
        return dto;
    }
}
