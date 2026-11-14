using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.FormasPagamento;

/// <summary>
/// Formas de pagamento do Bling de cada empresa: guarda a lista (renovada uma vez por dia, ou na
/// sincronização pedida pelo usuário) com o grupo de cada forma. Quem chama salva.
/// </summary>
public class FormasPagamentoBlingService
{
    public static readonly TimeSpan RenovarLista = TimeSpan.FromDays(1);

    private readonly IRepositorio<FormaPagamento> _formas;
    private readonly BlingClient _bling;
    private readonly FormasPagamentoOptions _opcoes;
    private readonly ILogger<FormasPagamentoBlingService> _logger;

    public FormasPagamentoBlingService(
        IRepositorio<FormaPagamento> formas,
        BlingClient bling,
        IOptions<FormasPagamentoOptions> opcoes,
        ILogger<FormasPagamentoBlingService> logger)
    {
        _formas = formas;
        _bling = bling;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public Task AtualizarSePrecisoAsync(Empresa empresa, string conta, string token, DateTime agoraUtc, CancellationToken cancelamento)
    {
        return AtualizarAsync(empresa, conta, token, agoraUtc, false, cancelamento);
    }

    /// <summary>
    /// Busca a lista de formas da empresa quando não tem, passou de um dia ou "forcar".
    /// Qualquer problema no Bling deixa a lista como está: a forma é um detalhe e não para a sincronização.
    /// </summary>
    public async Task AtualizarAsync(
        Empresa empresa, string conta, string token, DateTime agoraUtc, bool forcar, CancellationToken cancelamento)
    {
        List<FormaPagamento> guardadas = await _formas.Consultar()
            .Where(f => f.EmpresaId == empresa.Id)
            .ToListAsync(cancelamento);

        bool precisa = forcar || guardadas.Count == 0 || guardadas.Min(f => f.AtualizadaEm) < agoraUtc - RenovarLista;
        if (!precisa)
        {
            return;
        }

        List<FormaPagamentoBling> remotas;
        try
        {
            remotas = await _bling.ListarFormasPagamentoAsync(conta, token, cancelamento);
        }
        catch (BlingException erro)
        {
            if (erro.SemPermissao)
            {
                _logger.LogWarning("Empresa {EmpresaId}: o Bling não liberou as formas de pagamento (reconectar com o escopo novo)", empresa.Id);
            }
            else
            {
                _logger.LogWarning(erro, "Empresa {EmpresaId}: não consegui buscar as formas de pagamento no Bling", empresa.Id);
            }
            return;
        }
        catch (System.Text.Json.JsonException erro)
        {
            _logger.LogWarning(erro, "Empresa {EmpresaId}: resposta inesperada do Bling nas formas de pagamento", empresa.Id);
            return;
        }

        var porId = new Dictionary<long, FormaPagamento>();
        foreach (FormaPagamento guardada in guardadas)
        {
            porId[guardada.BlingId] = guardada;
        }

        List<string> termos = _opcoes.TermosDebitoAutomaticoValidos();
        var recebidas = new HashSet<long>();
        foreach (FormaPagamentoBling remota in remotas)
        {
            recebidas.Add(remota.Id);
            FormaPagamento? forma;
            if (!porId.TryGetValue(remota.Id, out forma))
            {
                forma = new FormaPagamento();
                forma.EmpresaId = empresa.Id;
                forma.BlingId = remota.Id;
                _formas.Adicionar(forma);
            }
            forma.Descricao = Limitar(remota.Descricao.Trim());
            forma.TipoPagamento = remota.TipoPagamento;
            forma.Situacao = remota.Situacao;
            forma.Grupo = GruposFormaPagamento.Classificar(remota.TipoPagamento, remota.Descricao, termos);
            forma.AtualizadaEm = agoraUtc;
        }

        foreach (FormaPagamento guardada in guardadas)
        {
            if (!recebidas.Contains(guardada.BlingId))
            {
                _formas.Remover(guardada);
            }
        }
    }

    private static string Limitar(string texto)
    {
        if (texto.Length > 200)
        {
            return texto.Substring(0, 200);
        }
        return texto;
    }
}
