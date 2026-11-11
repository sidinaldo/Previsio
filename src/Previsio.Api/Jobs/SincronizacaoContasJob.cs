using Hangfire;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Contratos;
using Previsio.Api.Excecoes;
using Previsio.Api.Servicos;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Jobs;

/// <summary>
/// Traz as contas a pagar de cada conta Bling para o banco do Previsio.
/// Inclui as lançadas à mão pela equipe, para o painel mostrar o total real.
/// </summary>
public class SincronizacaoContasJob
{
    private const int DiasPassados = 30;
    private const int DiasFuturos = 90;

    private readonly PrevisioDbContext _db;
    private readonly BlingClient _bling;
    private readonly TokenBlingService _tokens;
    private readonly ILogger<SincronizacaoContasJob> _logger;

    public SincronizacaoContasJob(
        PrevisioDbContext db,
        BlingClient bling,
        TokenBlingService tokens,
        ILogger<SincronizacaoContasJob> logger)
    {
        _db = db;
        _bling = bling;
        _tokens = tokens;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 900)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecutarAsync(CancellationToken cancelamento)
    {
        List<int> redes = await _db.Clientes
            .Where(PeriodoTeste.Processa(DateTime.UtcNow))
            .Select(c => c.Id)
            .ToListAsync(cancelamento);
        List<Empresa> empresas = await _db.Empresas
            .Where(e => e.Ativo && e.BlingRefreshTokenProtegido != null && redes.Contains(e.ClienteId))
            .ToListAsync(cancelamento);

        foreach (Empresa empresa in empresas)
        {
            try
            {
                await SincronizarEmpresaAsync(empresa, cancelamento);
            }
            catch (EmpresaDesconectadaException erro)
            {
                _logger.LogError(erro, "Empresa {EmpresaId} desconectada do Bling durante a sincronização", empresa.Id);

                // Renovação do token recusada pelo Bling: registra para aparecer na tela de falhas
                BlingException? causa = erro.InnerException as BlingException;
                if (causa != null)
                {
                    await RegistrarFalhaAsync(FabricaFalha.DaEmpresa(EtapaFalha.RenovacaoToken, causa, empresa), cancelamento);
                }
            }
            catch (Exception erro)
            {
                _logger.LogError(erro, "Erro ao sincronizar contas a pagar da empresa {EmpresaId}", empresa.Id);
            }
        }
    }

    private async Task RegistrarFalhaAsync(FalhaProcessamento falha, CancellationToken cancelamento)
    {
        try
        {
            _db.Falhas.Add(falha);
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Não foi possível registrar a falha da empresa {EmpresaId}", falha.EmpresaId);
        }
    }

    public async Task SincronizarEmpresaAsync(Empresa empresa, CancellationToken cancelamento)
    {
        string token = await _tokens.ObterAccessTokenAsync(empresa, cancelamento);
        string conta = TokenBlingService.IdentificadorConta(empresa);

        DateTime hoje = RelogioBrasil.Hoje();
        var filtro = new FiltroContasPagar();
        filtro.VencimentoInicial = hoje.AddDays(-DiasPassados);
        filtro.VencimentoFinal = hoje.AddDays(DiasFuturos);

        List<ContaPagarBling> remotas = await _bling.ListarContasPagarAsync(conta, token, filtro, cancelamento);

        List<ContaPagarSincronizada> locais = await _db.ContasPagar
            .Where(c => c.EmpresaId == empresa.Id
                && c.Vencimento >= filtro.VencimentoInicial
                && c.Vencimento <= filtro.VencimentoFinal)
            .ToListAsync(cancelamento);

        var locaisPorId = new Dictionary<long, ContaPagarSincronizada>();
        foreach (ContaPagarSincronizada local in locais)
        {
            locaisPorId[local.BlingId] = local;
        }

        var idsRecebidos = new HashSet<long>();
        DateTime agora = DateTime.UtcNow;

        foreach (ContaPagarBling remota in remotas)
        {
            DateTime vencimento;
            if (!DateTime.TryParseExact(remota.Vencimento, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out vencimento))
            {
                continue;
            }

            idsRecebidos.Add(remota.Id);

            ContaPagarSincronizada? local;
            if (!locaisPorId.TryGetValue(remota.Id, out local))
            {
                local = new ContaPagarSincronizada();
                local.EmpresaId = empresa.Id;
                local.BlingId = remota.Id;
                _db.ContasPagar.Add(local);
            }

            local.Vencimento = vencimento.Date;
            local.Valor = remota.Valor;
            local.Situacao = remota.Situacao;
            if (remota.Contato != null)
            {
                local.BlingContatoId = remota.Contato.Id;
            }
            local.SincronizadaEm = agora;
        }

        // Contas que sumiram do Bling dentro da janela foram excluídas lá
        foreach (ContaPagarSincronizada local in locais)
        {
            if (!idsRecebidos.Contains(local.BlingId))
            {
                _db.ContasPagar.Remove(local);
            }
        }

        empresa.UltimaSincronizacaoEm = agora;
        await _db.SaveChangesAsync(cancelamento);

        _logger.LogInformation("Empresa {Empresa}: {Quantidade} contas a pagar sincronizadas",
            empresa.Nome, remotas.Count);
    }
}
