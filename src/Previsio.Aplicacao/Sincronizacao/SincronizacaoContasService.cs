using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Core.Abstracoes;
using Previsio.Core.Contratos;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Falhas;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Conferencia;

namespace Previsio.Aplicacao.Sincronizacao;

/// <summary>
/// Traz as contas a pagar de cada conta Bling para o banco do Previsio.
/// Inclui as lançadas à mão pela equipe, para o painel mostrar o total real.
/// Janela e frequência vêm da seção "Sincronizacao" do appsettings.
/// </summary>
public class SincronizacaoContasService
{
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<ContaPagarSincronizada> _contas;
    private readonly IRepositorio<FalhaProcessamento> _falhas;
    private readonly BlingClient _bling;
    private readonly TokenBlingService _tokens;
    private readonly SincronizacaoOptions _opcoes;
    private readonly ContasAntigasEmAberto _antigas;
    private readonly FornecedoresBlingService _fornecedores;
    private readonly ConferenciaBlingService _conferencia;
    private readonly ILogger<SincronizacaoContasService> _logger;

    public SincronizacaoContasService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Empresa> empresas,
        IRepositorio<ContaPagarSincronizada> contas,
        IRepositorio<FalhaProcessamento> falhas,
        BlingClient bling,
        TokenBlingService tokens,
        IOptions<SincronizacaoOptions> opcoes,
        ContasAntigasEmAberto antigas,
        FornecedoresBlingService fornecedores,
        ConferenciaBlingService conferencia,
        ILogger<SincronizacaoContasService> logger)
    {
        _clientes = clientes;
        _empresas = empresas;
        _contas = contas;
        _falhas = falhas;
        _bling = bling;
        _tokens = tokens;
        _opcoes = opcoes.Value;
        _antigas = antigas;
        _fornecedores = fornecedores;
        _conferencia = conferencia;
        _logger = logger;
    }

    public async Task ExecutarAsync(CancellationToken cancelamento)
    {
        List<int> redes = await _clientes.Consultar()
            .Where(PeriodoTeste.Processa(DateTime.UtcNow))
            .Select(c => c.Id)
            .ToListAsync(cancelamento);
        List<Empresa> empresas = await _empresas.Consultar()
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
            _falhas.Adicionar(falha);
            await _falhas.SalvarAsync(cancelamento);
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
        filtro.VencimentoInicial = hoje.AddDays(-_opcoes.DiasPassadosValidos());
        filtro.VencimentoFinal = hoje.AddDays(_opcoes.DiasFuturosValidos());

        List<ContaPagarBling> remotas = await _bling.ListarContasPagarAsync(conta, token, filtro, cancelamento);

        List<ContaPagarSincronizada> locais = await _contas.Consultar()
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
        var atuais = new List<ContaPagarSincronizada>();
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
                _contas.Adicionar(local);
            }

            local.Vencimento = vencimento.Date;
            local.Valor = remota.Valor;
            local.Situacao = remota.Situacao;
            if (remota.Contato != null)
            {
                local.BlingContatoId = remota.Contato.Id;
            }
            local.SincronizadaEm = agora;
            atuais.Add(local);
        }

        // Contas que sumiram do Bling dentro da janela foram excluídas lá
        foreach (ContaPagarSincronizada local in locais)
        {
            if (!idsRecebidos.Contains(local.BlingId))
            {
                _contas.Remover(local);
            }
        }

        // Contas em aberto que venceram antes da janela: conferidas uma a uma até baixarem
        int antigasConferidas = await _antigas.ConferirAsync(
            empresa, conta, token, filtro.VencimentoInicial, _opcoes.MaximoContasAntigasValido(), agora, cancelamento);
        if (antigasConferidas > 0)
        {
            _logger.LogInformation("Empresa {Empresa}: {Quantidade} contas antigas em aberto conferidas no Bling",
                empresa.Nome, antigasConferidas);
        }

        // Nome do fornecedor: da nota ligada ou do contato do Bling (guardado)
        int contatosBuscados = await _fornecedores.PreencherNomesAsync(
            empresa, conta, token, atuais, _opcoes.DiasRenovarFornecedorValidos(), agora, cancelamento);
        if (contatosBuscados > 0)
        {
            _logger.LogInformation("Empresa {Empresa}: {Quantidade} fornecedores buscados no Bling",
                empresa.Nome, contatosBuscados);
        }

        empresa.UltimaSincronizacaoEm = agora;
        await _contas.SalvarAsync(cancelamento);

        _logger.LogInformation("Empresa {Empresa}: {Quantidade} contas a pagar sincronizadas",
            empresa.Nome, remotas.Count);

        // Com as contas em dia: as notas pendentes que alguém já lançou à mão fecham sozinhas
        await _conferencia.FecharPendentesAsync(empresa, conta, token, cancelamento);
    }
}
