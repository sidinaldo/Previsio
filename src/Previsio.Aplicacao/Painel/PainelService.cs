using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao.FormasPagamento;
using Previsio.Core.Entidades;
using Previsio.Core.Painel;
using Previsio.Core.Painel.Modelos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Notas;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Painel;

/// <summary>
/// Números do painel: contas a pagar dos próximos dias, por semana, por empresa e por dia,
/// e o fluxo de caixa (entradas × saídas) quando a rede usa.
/// </summary>
public class PainelService
{
    public const int DiasPadrao = 30;
    public const int MaximoDias = 120;

    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<ContaPagarSincronizada> _contas;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IRepositorio<ContaReceberSincronizada> _contasReceber;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<CategoriaFinanceira> _categorias;
    private readonly IRepositorio<FormaPagamento> _formas;
    private readonly CalculadoraPainel _calculadora;
    private readonly FormasPagamentoOptions _opcoesFormas;

    public PainelService(
        IRepositorio<Empresa> empresas,
        IRepositorio<ContaPagarSincronizada> contas,
        IRepositorio<NotaFiscal> notas,
        IRepositorio<ContaReceberSincronizada> contasReceber,
        IRepositorio<Cliente> clientes,
        IRepositorio<CategoriaFinanceira> categorias,
        IRepositorio<FormaPagamento> formas,
        CalculadoraPainel calculadora,
        IOptions<FormasPagamentoOptions> opcoesFormas)
    {
        _formas = formas;
        _opcoesFormas = opcoesFormas.Value;
        _empresas = empresas;
        _contas = contas;
        _notas = notas;
        _contasReceber = contasReceber;
        _clientes = clientes;
        _categorias = categorias;
        _calculadora = calculadora;
    }

    /// <summary>
    /// Período fora do intervalo aceito vira o padrão de 30 dias.
    /// </summary>
    public static int NormalizarDias(int dias)
    {
        if (dias <= 0 || dias > MaximoDias)
        {
            return DiasPadrao;
        }
        return dias;
    }

    /// <summary>
    /// Visão consolidada: total dos próximos N dias, por semana e por empresa.
    /// </summary>
    public async Task<ResumoPainel> ResumoAsync(int clienteId, int dias, CancellationToken cancelamento)
    {
        dias = NormalizarDias(dias);

        DateTime hoje = RelogioBrasil.Hoje();
        DateTime fim = hoje.AddDays(dias);

        List<Empresa> empresas = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == clienteId && e.Ativo)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);

        List<int> idsEmpresas = empresas.Select(e => e.Id).ToList();

        List<ContaPagarSincronizada> contas = await _contas.ConsultarSomenteLeitura()
            .Where(c => idsEmpresas.Contains(c.EmpresaId) && c.Vencimento >= hoje && c.Vencimento <= fim)
            .ToListAsync(cancelamento);

        List<NotaFiscal> pendentes = await _notas.ConsultarSomenteLeitura()
            .Include(n => n.Parcelas)
            .Where(n => n.ClienteId == clienteId && StatusPendentes.Todos.Contains(n.Status))
            .ToListAsync(cancelamento);

        ResumoPainel resumo = _calculadora.Calcular(hoje, dias, empresas, contas, pendentes);

        // Saídas por categoria: nome da categoria no Bling de cada empresa
        var nomesCategorias = new Dictionary<string, string>();
        var categorias = await _categorias.ConsultarSomenteLeitura()
            .Where(k => idsEmpresas.Contains(k.EmpresaId))
            .Select(k => new { k.EmpresaId, k.BlingId, k.Descricao })
            .ToListAsync(cancelamento);
        foreach (var categoria in categorias)
        {
            nomesCategorias[categoria.EmpresaId + ":" + categoria.BlingId] = categoria.Descricao;
        }
        _calculadora.AplicarCategorias(resumo, contas, nomesCategorias);

        // Como as contas saem: grupo da forma de pagamento no Bling de cada empresa
        var gruposFormas = new Dictionary<string, string>();
        var formas = await _formas.ConsultarSomenteLeitura()
            .Where(f => idsEmpresas.Contains(f.EmpresaId))
            .Select(f => new { f.EmpresaId, f.BlingId, f.Grupo })
            .ToListAsync(cancelamento);
        foreach (var forma in formas)
        {
            gruposFormas[forma.EmpresaId + ":" + forma.BlingId] = forma.Grupo;
        }
        _calculadora.AplicarFormas(resumo, contas, gruposFormas, _opcoesFormas.GruposSaiSozinhoValidos());

        // Vencidas em aberto (a sincronização traz os últimos 30 dias)
        List<ContaPagarSincronizada> vencidas = await _contas.ConsultarSomenteLeitura()
            .Where(c => idsEmpresas.Contains(c.EmpresaId)
                && c.Vencimento < hoje
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago))
            .ToListAsync(cancelamento);
        resumo.QuantidadeVencidas = vencidas.Count;
        resumo.TotalVencidas = vencidas.Sum(c => c.Valor);

        // Fluxo de caixa: contas a receber do período e as vencidas da janela sincronizada
        bool fluxoCaixa = await _clientes.ConsultarSomenteLeitura()
            .Where(c => c.Id == clienteId)
            .Select(c => c.MostrarFluxoCaixa)
            .FirstOrDefaultAsync(cancelamento);
        if (fluxoCaixa)
        {
            List<ContaReceberSincronizada> receber = await _contasReceber.ConsultarSomenteLeitura()
                .Where(c => idsEmpresas.Contains(c.EmpresaId) && c.Vencimento <= fim)
                .ToListAsync(cancelamento);
            _calculadora.AplicarEntradas(resumo, receber);
        }
        return resumo;
    }

    /// <summary>
    /// Vencimentos em aberto de uma empresa agrupados por dia.
    /// </summary>
    public async Task<List<VencimentoDiaDto>> VencimentosAsync(int empresaId, int dias, CancellationToken cancelamento)
    {
        dias = NormalizarDias(dias);

        DateTime hoje = RelogioBrasil.Hoje();
        DateTime fim = hoje.AddDays(dias);

        List<ContaPagarSincronizada> contas = await _contas.ConsultarSomenteLeitura()
            .Where(c => c.EmpresaId == empresaId
                && c.Vencimento >= hoje
                && c.Vencimento <= fim
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago))
            .ToListAsync(cancelamento);

        return contas
            .GroupBy(c => c.Vencimento.Date)
            .OrderBy(g => g.Key)
            .Select(g => new VencimentoDiaDto
            {
                Data = g.Key,
                Quantidade = g.Count(),
                Valor = g.Sum(c => c.Valor)
            })
            .ToList();
    }
}
