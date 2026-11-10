using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Controllers;

/// <summary>
/// Histórico das falhas ao ler notas do e-mail e ao lançá-las no Bling.
/// </summary>
[ApiController]
public class FalhasController : ControllerBase
{
    public const int TamanhoPagina = 50;

    // Mesmo limite dos filtros de período da API do Bling
    private const int MaximoDiasPeriodo = 366;

    private readonly PrevisioDbContext _db;

    public FalhasController(PrevisioDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Falhas do cliente, da mais recente para a mais antiga.
    /// Filtros opcionais: empresa, nota, etapa e período (datas no formato yyyy-MM-dd, horário de Brasília).
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/falhas")]
    public async Task<ActionResult<PaginaFalhasDto>> Listar(
        int clienteId,
        [FromQuery] int? empresaId,
        [FromQuery] int? notaId,
        [FromQuery] string? etapa,
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

        IQueryable<FalhaProcessamento> consulta = _db.Falhas.Where(f => f.ClienteId == clienteId);

        if (empresaId.HasValue)
        {
            consulta = consulta.Where(f => f.EmpresaId == empresaId.Value);
        }

        if (notaId.HasValue)
        {
            consulta = consulta.Where(f => f.NotaFiscalId == notaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(etapa))
        {
            EtapaFalha etapaEscolhida;
            if (!Enum.TryParse(etapa, true, out etapaEscolhida) || !Enum.IsDefined(typeof(EtapaFalha), etapaEscolhida))
            {
                return BadRequest("Etapa inválida.");
            }
            consulta = consulta.Where(f => f.Etapa == etapaEscolhida);
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

        // As datas chegam como dia no horário de Brasília; no banco tudo é UTC
        if (de.HasValue)
        {
            DateTime inicioUtc = InicioDoDiaEmUtc(de.Value);
            consulta = consulta.Where(f => f.OcorridaEm >= inicioUtc);
        }

        if (ate.HasValue)
        {
            DateTime fimUtc = InicioDoDiaEmUtc(ate.Value.AddDays(1));
            consulta = consulta.Where(f => f.OcorridaEm < fimUtc);
        }

        int total = await consulta.CountAsync(cancelamento);

        List<FalhaProcessamento> falhas = await consulta
            .Include(f => f.Empresa)
            .OrderByDescending(f => f.OcorridaEm)
            .ThenByDescending(f => f.Id)
            .Skip((pagina - 1) * TamanhoPagina)
            .Take(TamanhoPagina)
            .ToListAsync(cancelamento);

        var resposta = new PaginaFalhasDto();
        resposta.Total = total;
        resposta.Pagina = pagina;
        resposta.TamanhoPagina = TamanhoPagina;
        foreach (FalhaProcessamento falha in falhas)
        {
            resposta.Itens.Add(ParaDto(falha));
        }
        return resposta;
    }

    public static FalhaDto ParaDto(FalhaProcessamento falha)
    {
        var dto = new FalhaDto();
        dto.Id = falha.Id;
        dto.OcorridaEm = falha.OcorridaEm;
        dto.Etapa = falha.Etapa.ToString();
        dto.Mensagem = falha.Mensagem;
        dto.Metodo = falha.Metodo;
        dto.Endereco = falha.Endereco;
        dto.StatusHttp = falha.StatusHttp;
        dto.Resposta = falha.Resposta;
        dto.EmpresaId = falha.EmpresaId;
        if (falha.Empresa != null)
        {
            dto.EmpresaNome = falha.Empresa.Nome;
        }
        dto.NotaFiscalId = falha.NotaFiscalId;
        dto.NumeroNota = falha.NumeroNota;
        dto.ChaveAcesso = falha.ChaveAcesso;
        dto.Fornecedor = falha.Fornecedor;
        dto.CnpjFornecedor = falha.CnpjFornecedor;
        dto.ValorNota = falha.ValorNota;
        dto.NumeroParcela = falha.NumeroParcela;
        dto.ValorParcela = falha.ValorParcela;
        dto.VencimentoParcela = falha.VencimentoParcela;
        dto.Origem = falha.Origem;
        return dto;
    }

    private static DateTime InicioDoDiaEmUtc(DateTime dia)
    {
        DateTime inicioLocal = DateTime.SpecifyKind(dia.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(inicioLocal, RelogioBrasil.Fuso);
    }
}
