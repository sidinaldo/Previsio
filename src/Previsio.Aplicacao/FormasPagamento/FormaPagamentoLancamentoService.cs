using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.FormasPagamento;

/// <summary>
/// Escolhe a forma de pagamento do Bling para as contas de uma nota, nesta ordem:
/// 1) regra da rede para o fornecedor (pelo nome da forma);
/// 2) a forma da última conta desse fornecedor no Bling da empresa (aprende com quem corrigiu lá);
/// 3) o meio de pagamento do XML (tPag), pela forma do Bling com o mesmo tipo;
/// 4) a forma padrão da rede ou, sem ela, o padrão automático (FormasPagamento:PadraoAutomatico).
/// Só valem formas ativas no Bling da empresa. Se um passo não acha a forma lá, tenta o seguinte;
/// se nenhum acha, a conta vai sem forma.
/// </summary>
public class FormaPagamentoLancamentoService
{
    private readonly IRepositorio<RegraFormaPagamento> _regras;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<ContaPagarSincronizada> _contas;
    private readonly IRepositorio<FormaPagamento> _formas;
    private readonly FormasPagamentoBlingService _formasBling;
    private readonly FormasPagamentoOptions _opcoes;

    public FormaPagamentoLancamentoService(
        IRepositorio<RegraFormaPagamento> regras,
        IRepositorio<Cliente> clientes,
        IRepositorio<ContaPagarSincronizada> contas,
        IRepositorio<FormaPagamento> formas,
        FormasPagamentoBlingService formasBling,
        IOptions<FormasPagamentoOptions> opcoes)
    {
        _regras = regras;
        _clientes = clientes;
        _contas = contas;
        _formas = formas;
        _formasBling = formasBling;
        _opcoes = opcoes.Value;
    }

    /// <summary>Id da forma no Bling da empresa, ou null para lançar sem forma.</summary>
    public async Task<long?> EscolherAsync(
        NotaFiscal nota, Empresa empresa, long contatoId, string conta, string token, CancellationToken cancelamento)
    {
        await _formasBling.AtualizarSePrecisoAsync(empresa, conta, token, DateTime.UtcNow, cancelamento);
        await _formas.SalvarAsync(cancelamento);

        int empresaId = empresa.Id;
        List<FormaPagamento> ativas = await _formas.ConsultarSomenteLeitura()
            .Where(f => f.EmpresaId == empresaId && f.Situacao == 1)
            .OrderBy(f => f.BlingId)
            .ToListAsync(cancelamento);
        if (ativas.Count == 0)
        {
            return null;
        }

        // 1) Regra da rede para o fornecedor
        string cnpj = new string((nota.CnpjFornecedor ?? "").Where(char.IsDigit).ToArray());
        int clienteId = nota.ClienteId;
        string? regra = await _regras.ConsultarSomenteLeitura()
            .Where(r => r.ClienteId == clienteId && r.CnpjFornecedor == cnpj)
            .Select(r => r.Forma)
            .FirstOrDefaultAsync(cancelamento);
        if (regra != null)
        {
            FormaPagamento? daRegra = PorNome(ativas, regra);
            if (daRegra != null)
            {
                return daRegra.BlingId;
            }
        }

        // 2) Última conta desse fornecedor no Bling da empresa com forma
        List<long?> anteriores = await _contas.ConsultarSomenteLeitura()
            .Where(c => c.EmpresaId == empresaId && c.BlingContatoId == contatoId && c.FormaPagamentoBlingId != null)
            .OrderByDescending(c => c.Vencimento)
            .ThenByDescending(c => c.Id)
            .Select(c => c.FormaPagamentoBlingId)
            .Take(5)
            .ToListAsync(cancelamento);
        foreach (long? anterior in anteriores)
        {
            if (anterior.HasValue && ativas.Any(f => f.BlingId == anterior.Value))
            {
                return anterior.Value;
            }
        }

        // 3) Meio de pagamento do XML
        if (nota.TipoPagamento.HasValue)
        {
            FormaPagamento? doXml = PorTipo(ativas, nota.TipoPagamento.Value);
            if (doXml != null)
            {
                return doXml.BlingId;
            }
        }

        // 4) Forma padrão da rede (sem ela, a automática)
        string? padrao = await _clientes.ConsultarSomenteLeitura()
            .Where(c => c.Id == clienteId)
            .Select(c => c.FormaPagamentoPadrao)
            .FirstOrDefaultAsync(cancelamento);
        if (string.IsNullOrWhiteSpace(padrao))
        {
            padrao = _opcoes.PadraoAutomaticoValido();
        }
        if (padrao == null)
        {
            return null;
        }
        FormaPagamento? daPadrao = PorNome(ativas, padrao);
        if (daPadrao == null)
        {
            return null;
        }
        return daPadrao.BlingId;
    }

    private static FormaPagamento? PorNome(List<FormaPagamento> formas, string nome)
    {
        string procurado = GruposFormaPagamento.Normalizar(nome);
        return formas.FirstOrDefault(f => GruposFormaPagamento.Normalizar(f.Descricao) == procurado);
    }

    /// <summary>
    /// A forma do Bling com o tipo (tPag) da nota. Débito automático fica de fora: a nota dizer
    /// "boleto" não quer dizer que a empresa debita esse fornecedor na conta. Havendo mais de uma,
    /// prefere a de nome igual ao do grupo ("Boleto", "Pix").
    /// </summary>
    private static FormaPagamento? PorTipo(List<FormaPagamento> formas, int tipoPagamento)
    {
        List<FormaPagamento> doTipo = formas
            .Where(f => f.TipoPagamento == tipoPagamento && f.Grupo != GruposFormaPagamento.DebitoAutomatico)
            .ToList();
        if (doTipo.Count == 0)
        {
            return null;
        }
        foreach (FormaPagamento forma in doTipo)
        {
            if (GruposFormaPagamento.Normalizar(forma.Descricao) == GruposFormaPagamento.Normalizar(GruposFormaPagamento.Nome(forma.Grupo)))
            {
                return forma;
            }
        }
        return doTipo[0];
    }
}
