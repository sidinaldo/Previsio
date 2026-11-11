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
    public const int TamanhoPagina = 50;

    // Filtro de situação que junta todas as que ainda não viraram conta a pagar
    public const string SituacaoPendentes = "Pendentes";

    // Mesmo limite do filtro de período das falhas
    private const int MaximoDiasPeriodo = 366;

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

    /// <summary>
    /// Todas as notas lidas do e-mail (lançadas ou não), da recebida mais recente para a mais antiga, com as parcelas.
    /// Filtros opcionais: empresa, situação (nome da situação ou "Pendentes") e período em que a nota
    /// chegou (datas no formato yyyy-MM-dd, horário de Brasília).
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/notas")]
    public async Task<ActionResult<PaginaNotasDto>> Listar(
        int clienteId,
        [FromQuery] int? empresaId,
        [FromQuery] string? situacao,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int pagina,
        CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        if (pagina < 1)
        {
            pagina = 1;
        }

        IQueryable<NotaFiscal> consulta = _db.NotasFiscais.Where(n => n.ClienteId == clienteId);

        if (empresaId.HasValue)
        {
            consulta = consulta.Where(n => n.EmpresaId == empresaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(situacao))
        {
            if (string.Equals(situacao, SituacaoPendentes, StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta.Where(n => StatusPendentes.Todos.Contains(n.Status));
            }
            else
            {
                StatusNota escolhida;
                if (!Enum.TryParse(situacao, true, out escolhida) || !Enum.IsDefined(typeof(StatusNota), escolhida))
                {
                    return BadRequest("Situação inválida.");
                }
                consulta = consulta.Where(n => n.Status == escolhida);
            }
        }

        if (de.HasValue && ate.HasValue)
        {
            if (ate.Value.Date < de.Value.Date)
            {
                return BadRequest("A data final deve ser igual ou posterior à inicial.");
            }
            if ((ate.Value.Date - de.Value.Date).TotalDays > MaximoDiasPeriodo)
            {
                return BadRequest("O período pode ter no máximo um ano.");
            }
        }

        if (de.HasValue)
        {
            DateTime inicioUtc = RelogioBrasil.InicioDoDiaEmUtc(de.Value);
            consulta = consulta.Where(n => n.RecebidaEm >= inicioUtc);
        }

        if (ate.HasValue)
        {
            DateTime fimUtc = RelogioBrasil.InicioDoDiaEmUtc(ate.Value.AddDays(1));
            consulta = consulta.Where(n => n.RecebidaEm < fimUtc);
        }

        int total = await consulta.CountAsync(cancelamento);

        List<NotaFiscal> notas = await consulta
            .Include(n => n.Empresa)
            .Include(n => n.Parcelas)
            .OrderByDescending(n => n.RecebidaEm)
            .ThenByDescending(n => n.Id)
            .Skip((pagina - 1) * TamanhoPagina)
            .Take(TamanhoPagina)
            .ToListAsync(cancelamento);

        var resposta = new PaginaNotasDto();
        resposta.Total = total;
        resposta.Pagina = pagina;
        resposta.TamanhoPagina = TamanhoPagina;
        foreach (NotaFiscal nota in notas)
        {
            resposta.Itens.Add(ParaNotaRecebidaDto(nota));
        }
        return resposta;
    }

    public static NotaRecebidaDto ParaNotaRecebidaDto(NotaFiscal nota)
    {
        var dto = new NotaRecebidaDto();
        dto.Id = nota.Id;
        dto.EmpresaId = nota.EmpresaId;
        if (nota.Empresa != null)
        {
            dto.EmpresaNome = nota.Empresa.Nome;
        }
        dto.Numero = nota.Numero;
        dto.Serie = nota.Serie;
        dto.ChaveAcesso = nota.ChaveAcesso;
        dto.Fornecedor = nota.NomeFornecedor;
        dto.CnpjFornecedor = nota.CnpjFornecedor;
        dto.DataEmissao = nota.DataEmissao;
        dto.ValorTotal = nota.ValorTotal;
        dto.Status = nota.Status.ToString();
        dto.MensagemErro = nota.MensagemErro;
        dto.RecebidaEm = nota.RecebidaEm;
        dto.LancadaEm = nota.LancadaEm;

        List<ParcelaNota> parcelas = nota.Parcelas
            .OrderBy(p => p.Vencimento)
            .ThenBy(p => p.NumeroParcela)
            .ToList();
        if (parcelas.Count > 0)
        {
            dto.PrimeiroVencimento = parcelas[0].Vencimento;
        }
        foreach (ParcelaNota parcela in parcelas)
        {
            var parcelaDto = new ParcelaNotaDto();
            parcelaDto.NumeroParcela = parcela.NumeroParcela;
            parcelaDto.Vencimento = parcela.Vencimento;
            parcelaDto.Valor = parcela.Valor;
            parcelaDto.BlingContaPagarId = parcela.BlingContaPagarId;
            dto.Parcelas.Add(parcelaDto);
        }
        return dto;
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
