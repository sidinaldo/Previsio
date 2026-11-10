using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Jobs;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.Painel;
using Previsio.Core.Painel.Modelos;
using Previsio.Data;

namespace Previsio.Api.Controllers;

[ApiController]
public class PainelController : ControllerBase
{
    private static readonly StatusNota[] StatusPendentes = new StatusNota[]
    {
        StatusNota.Recebida,
        StatusNota.SemParcelas,
        StatusNota.EmpresaNaoEncontrada,
        StatusNota.BlingDesconectado,
        StatusNota.Erro
    };

    private readonly PrevisioDbContext _db;
    private readonly CalculadoraPainel _calculadora;
    private readonly IBackgroundJobClient _jobs;

    public PainelController(PrevisioDbContext db, CalculadoraPainel calculadora, IBackgroundJobClient jobs)
    {
        _db = db;
        _calculadora = calculadora;
        _jobs = jobs;
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
            .Where(n => n.ClienteId == clienteId && StatusPendentes.Contains(n.Status))
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

    /// <summary>
    /// Notas que chegaram no e-mail e ainda não viraram conta a pagar no Bling.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/notas/pendentes")]
    public async Task<ActionResult<List<NotaDto>>> NotasPendentes(int clienteId, [FromQuery] int? empresaId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        IQueryable<NotaFiscal> consulta = _db.NotasFiscais
            .Include(n => n.Empresa)
            .Include(n => n.Parcelas)
            .Where(n => n.ClienteId == clienteId && StatusPendentes.Contains(n.Status));

        if (empresaId.HasValue)
        {
            consulta = consulta.Where(n => n.EmpresaId == empresaId.Value);
        }

        List<NotaFiscal> notas = await consulta
            .OrderBy(n => n.RecebidaEm)
            .Take(500)
            .ToListAsync(cancelamento);

        var lista = new List<NotaDto>();
        foreach (NotaFiscal nota in notas)
        {
            var dto = new NotaDto();
            dto.Id = nota.Id;
            dto.EmpresaId = nota.EmpresaId;
            if (nota.Empresa != null)
            {
                dto.EmpresaNome = nota.Empresa.Nome;
            }
            dto.Numero = nota.Numero;
            dto.ChaveAcesso = nota.ChaveAcesso;
            dto.Fornecedor = nota.NomeFornecedor;
            dto.CnpjFornecedor = nota.CnpjFornecedor;
            dto.DataEmissao = nota.DataEmissao;
            dto.ValorTotal = nota.ValorTotal;
            if (nota.Parcelas.Count > 0)
            {
                dto.PrimeiroVencimento = nota.Parcelas.Min(p => p.Vencimento);
            }
            dto.Status = nota.Status.ToString();
            dto.MensagemErro = nota.MensagemErro;
            dto.RecebidaEm = nota.RecebidaEm;
            lista.Add(dto);
        }
        return lista;
    }

    [HttpPost("api/notas/{notaId:int}/reprocessar")]
    public async Task<IActionResult> Reprocessar(int notaId, CancellationToken cancelamento)
    {
        NotaFiscal? nota = await _db.NotasFiscais
            .Include(n => n.Parcelas)
            .FirstOrDefaultAsync(n => n.Id == notaId, cancelamento);

        // Nota de outro cliente responde igual a inexistente, para não revelar ids
        if (nota == null || !User.PodeAcessarCliente(nota.ClienteId))
        {
            return NotFound();
        }
        if (nota.Status == StatusNota.Lancada)
        {
            return BadRequest("A nota já foi lançada.");
        }
        if (nota.EmpresaId == null || nota.Parcelas.Count == 0)
        {
            return BadRequest("A nota não tem empresa ou parcelas para lançar.");
        }

        nota.Status = StatusNota.Recebida;
        nota.Tentativas = 0;
        nota.MensagemErro = null;
        await _db.SaveChangesAsync(cancelamento);

        _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarAsync(notaId, CancellationToken.None));
        return Accepted();
    }

    /// <summary>
    /// Dispara manualmente um dos jobs (útil em testes e no piloto).
    /// Só administrador: cada job percorre todos os clientes.
    /// </summary>
    [HttpPost("api/jobs/{nome}/executar")]
    [Authorize(Roles = Perfis.Admin)]
    public IActionResult ExecutarJob(string nome)
    {
        if (nome == "leitura-email")
        {
            _jobs.Enqueue<LeituraEmailJob>(j => j.ExecutarAsync(CancellationToken.None));
        }
        else if (nome == "lancamento")
        {
            _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarPendentesAsync(CancellationToken.None));
        }
        else if (nome == "sincronizacao")
        {
            _jobs.Enqueue<SincronizacaoContasJob>(j => j.ExecutarAsync(CancellationToken.None));
        }
        else
        {
            return NotFound("Use: leitura-email, lancamento ou sincronizacao.");
        }
        return Accepted();
    }
}
