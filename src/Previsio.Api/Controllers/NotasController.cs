using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Jobs;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Controllers;

[ApiController]
public class NotasController : ControllerBase
{
    private readonly PrevisioDbContext _db;
    private readonly IBackgroundJobClient _jobs;

    public NotasController(PrevisioDbContext db, IBackgroundJobClient jobs)
    {
        _db = db;
        _jobs = jobs;
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
            .Where(n => n.ClienteId == clienteId && StatusPendentes.Todos.Contains(n.Status));

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
}
