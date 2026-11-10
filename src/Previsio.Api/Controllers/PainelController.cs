using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.Painel;
using Previsio.Core.Painel.Modelos;
using Previsio.Data;

namespace Previsio.Api.Controllers;

[ApiController]
public class PainelController : ControllerBase
{
    private readonly PrevisioDbContext _db;
    private readonly CalculadoraPainel _calculadora;

    public PainelController(PrevisioDbContext db, CalculadoraPainel calculadora)
    {
        _db = db;
        _calculadora = calculadora;
    }

    /// <summary>
    /// Visão consolidada: total dos próximos N dias, por semana e por empresa.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/painel")]
    public async Task<ActionResult<ResumoPainel>> Resumo(int clienteId, [FromQuery] int dias, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        if (dias <= 0 || dias > 120)
        {
            dias = 30;
        }

        DateTime hoje = RelogioBrasil.Hoje();
        DateTime fim = hoje.AddDays(dias);

        List<Empresa> empresas = await _db.Empresas
            .Where(e => e.ClienteId == clienteId && e.Ativo)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);

        List<int> idsEmpresas = empresas.Select(e => e.Id).ToList();

        List<ContaPagarSincronizada> contas = await _db.ContasPagar
            .Where(c => idsEmpresas.Contains(c.EmpresaId) && c.Vencimento >= hoje && c.Vencimento <= fim)
            .ToListAsync(cancelamento);

        List<NotaFiscal> pendentes = await _db.NotasFiscais
            .Where(n => n.ClienteId == clienteId && StatusPendentes.Todos.Contains(n.Status))
            .ToListAsync(cancelamento);

        return _calculadora.Calcular(hoje, dias, empresas, contas, pendentes);
    }

    /// <summary>
    /// Vencimentos de uma empresa agrupados por dia.
    /// </summary>
    [HttpGet("api/empresas/{empresaId:int}/vencimentos")]
    public async Task<ActionResult<List<VencimentoDiaDto>>> Vencimentos(int empresaId, [FromQuery] int dias, CancellationToken cancelamento)
    {
        bool permitido = await ControleAcesso.PodeAcessarEmpresaAsync(_db, User, empresaId, cancelamento);
        if (!permitido)
        {
            return NotFound("Empresa não encontrada.");
        }

        if (dias <= 0 || dias > 120)
        {
            dias = 30;
        }

        DateTime hoje = RelogioBrasil.Hoje();
        DateTime fim = hoje.AddDays(dias);

        List<ContaPagarSincronizada> contas = await _db.ContasPagar
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
