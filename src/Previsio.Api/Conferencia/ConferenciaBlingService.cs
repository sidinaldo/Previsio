using Microsoft.EntityFrameworkCore;
using Previsio.Api.Servicos;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Conferencia;

namespace Previsio.Api.Conferencia;

/// <summary>
/// Conferência com o Bling: evita lançar de novo a nota que alguém já lançou à mão e fecha
/// sozinhas as notas pendentes que já estão no Bling. Encontro parcial nunca lança: vira aviso.
/// </summary>
public class ConferenciaBlingService
{
    public const string ResolvidaPor = "Conferência com o Bling";

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IRepositorio<ContaPagarSincronizada> _contasPagar;
    private readonly IRepositorio<ParcelaNota> _parcelas;
    private readonly BlingClient _bling;

    public ConferenciaBlingService(
        IRepositorio<Cliente> clientes,
        IRepositorio<NotaFiscal> notas,
        IRepositorio<ContaPagarSincronizada> contasPagar,
        IRepositorio<ParcelaNota> parcelas,
        BlingClient bling)
    {
        _clientes = clientes;
        _notas = notas;
        _contasPagar = contasPagar;
        _parcelas = parcelas;
        _bling = bling;
    }

    /// <summary>
    /// Antes de lançar: procura no Bling, agora, as contas do fornecedor nas datas das parcelas.
    /// </summary>
    public async Task<ResultadoConferencia> ConferirNoBlingAsync(
        NotaFiscal nota, Cliente cliente, string conta, string token, long contatoId, CancellationToken cancelamento)
    {
        RegrasConferencia regras = RegrasConferencia.Da(cliente);
        var filtro = new FiltroContasPagar();
        if (nota.Parcelas.Count == 0)
        {
            filtro.VencimentoInicial = nota.DataEmissao.Date;
            filtro.VencimentoFinal = nota.DataEmissao.Date.AddDays(ConferenciaBling.DiasNotaSemParcelas);
        }
        else
        {
            filtro.VencimentoInicial = nota.Parcelas.Min(p => p.Vencimento).Date.AddDays(-regras.ToleranciaDias);
            filtro.VencimentoFinal = nota.Parcelas.Max(p => p.Vencimento).Date.AddDays(regras.ToleranciaDias);
        }

        List<ContaPagarBling> remotas = await _bling.ListarContasPagarAsync(conta, token, filtro, cancelamento);
        var candidatas = new List<ContaNoBling>();
        foreach (ContaPagarBling remota in remotas)
        {
            DateTime vencimento;
            if (remota.Contato == null || remota.Contato.Id != contatoId
                || !DateTime.TryParseExact(remota.Vencimento, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out vencimento))
            {
                continue;
            }
            var candidata = new ContaNoBling();
            candidata.Id = remota.Id;
            candidata.ContatoId = remota.Contato.Id;
            candidata.Vencimento = vencimento;
            candidata.Valor = remota.Valor;
            candidata.Situacao = remota.Situacao;
            candidata.NumeroDocumento = remota.NumeroDocumento;
            candidata.Historico = remota.Historico;
            candidatas.Add(candidata);
        }

        candidatas = await SemAsJaVinculadasAsync(candidatas, nota.Id, cancelamento);
        return await ConferenciaBling.ConferirAsync(nota, candidatas, regras,
            candidata => CompletarAsync(candidata, conta, token, cancelamento));
    }

    /// <summary>
    /// Depois da sincronização da empresa: as notas pendentes que já estão no Bling fecham sozinhas;
    /// as encontradas em parte ganham o motivo para alguém conferir.
    /// </summary>
    public async Task FecharPendentesAsync(Empresa empresa, string conta, string token, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == empresa.ClienteId, cancelamento);
        if (cliente == null || !cliente.ConferenciaFecharPendentes)
        {
            return;
        }
        RegrasConferencia regras = RegrasConferencia.Da(cliente);

        List<NotaFiscal> pendentes = await _notas.Consultar()
            .Include(n => n.Parcelas)
            .Where(n => n.EmpresaId == empresa.Id && (n.Status == StatusNota.SemParcelas || n.Status == StatusNota.Erro))
            .ToListAsync(cancelamento);

        var contatos = new Dictionary<string, long?>();
        foreach (NotaFiscal nota in pendentes)
        {
            // Nota com parcela já lançada pelo Previsio: o resto é com o lançamento, não com a conferência
            if (nota.Parcelas.Any(p => p.BlingContaPagarId.HasValue))
            {
                continue;
            }
            if (!contatos.ContainsKey(nota.CnpjFornecedor))
            {
                ContatoBling? contato = await _bling.BuscarContatoPorDocumentoAsync(conta, token, nota.CnpjFornecedor, cancelamento);
                contatos[nota.CnpjFornecedor] = contato == null ? null : contato.Id;
            }
            long? contatoId = contatos[nota.CnpjFornecedor];
            if (!contatoId.HasValue)
            {
                continue;
            }

            long id = contatoId.Value;
            List<ContaNoBling> candidatas = await _contasPagar.ConsultarSomenteLeitura()
                .Where(c => c.EmpresaId == empresa.Id && c.BlingContatoId == id)
                .Select(c => new ContaNoBling { Id = c.BlingId, ContatoId = c.BlingContatoId, Vencimento = c.Vencimento, Valor = c.Valor, Situacao = c.Situacao })
                .ToListAsync(cancelamento);
            candidatas = await SemAsJaVinculadasAsync(candidatas, nota.Id, cancelamento);

            ResultadoConferencia resultado = await ConferenciaBling.ConferirAsync(nota, candidatas, regras,
                candidata => CompletarAsync(candidata, conta, token, cancelamento));
            if (resultado.Completa)
            {
                MarcarEncontrada(nota, resultado);
            }
            else if (resultado.Parcial)
            {
                nota.MensagemErro = MensagemParcial(resultado);
            }
        }
        await _clientes.SalvarAsync(cancelamento);
    }

    /// <summary>
    /// Todas as parcelas estão no Bling: a nota fica como lançada à mão, ligada às contas encontradas.
    /// </summary>
    public static void MarcarEncontrada(NotaFiscal nota, ResultadoConferencia resultado)
    {
        foreach (ParcelaEncontrada encontrada in resultado.Encontradas)
        {
            if (encontrada.Parcela != null)
            {
                encontrada.Parcela.BlingContaPagarId = encontrada.ContaId;
            }
        }
        nota.Status = StatusNota.LancadaManualmente;
        nota.MensagemErro = null;
        nota.ResolvidaManualmenteEm = DateTime.UtcNow;
        nota.ResolvidaManualmentePor = ResolvidaPor;
        nota.ObservacaoResolucao = "Encontrada no Bling, lançada à mão (conta " + resultado.Contas() + ").";
    }

    public static string MensagemParcial(ResultadoConferencia resultado)
    {
        return "Achei " + resultado.Encontradas.Count + " de " + resultado.Esperadas + " parcelas já lançadas no Bling (conta "
            + resultado.Contas() + "). Confira no Bling, lance o que falta à mão e marque como lançada.";
    }

    // Contas que o Previsio já ligou a outra nota não servem de candidatas
    private async Task<List<ContaNoBling>> SemAsJaVinculadasAsync(List<ContaNoBling> candidatas, int notaId, CancellationToken cancelamento)
    {
        if (candidatas.Count == 0)
        {
            return candidatas;
        }
        List<long> ids = candidatas.Select(c => c.Id).ToList();
        List<long> vinculadas = await _parcelas.ConsultarSomenteLeitura()
            .Where(p => p.NotaFiscalId != notaId && p.BlingContaPagarId.HasValue && ids.Contains(p.BlingContaPagarId.Value))
            .Select(p => p.BlingContaPagarId!.Value)
            .ToListAsync(cancelamento);
        return candidatas.Where(c => !vinculadas.Contains(c.Id)).ToList();
    }

    private async Task CompletarAsync(ContaNoBling candidata, string conta, string token, CancellationToken cancelamento)
    {
        ContaPagarBling? detalhe = await _bling.ObterContaPagarAsync(conta, token, candidata.Id, cancelamento);
        if (detalhe != null)
        {
            candidata.NumeroDocumento = detalhe.NumeroDocumento;
            candidata.Historico = detalhe.Historico;
        }
    }
}
