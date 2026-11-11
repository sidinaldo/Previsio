using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;

namespace Previsio.Api.Servicos;

/// <summary>
/// Histórico das falhas ao ler notas do e-mail e ao lançá-las no Bling.
/// </summary>
public class FalhasService
{
    public const int TamanhoPagina = 50;

    private readonly IRepositorio<FalhaProcessamento> _falhas;

    public FalhasService(IRepositorio<FalhaProcessamento> falhas)
    {
        _falhas = falhas;
    }

    /// <summary>
    /// Falhas do cliente, da mais recente para a mais antiga.
    /// Filtros opcionais: empresa, nota, etapa e período (dias no horário de Brasília).
    /// </summary>
    public async Task<Resultado<PaginaFalhasDto>> ListarAsync(
        int clienteId,
        int? empresaId,
        int? notaId,
        string? etapa,
        DateTime? de,
        DateTime? ate,
        int pagina,
        CancellationToken cancelamento)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        IQueryable<FalhaProcessamento> consulta = _falhas.ConsultarSomenteLeitura().Where(f => f.ClienteId == clienteId);

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
                return Resultado<PaginaFalhasDto>.Invalido("Etapa inválida.");
            }
            consulta = consulta.Where(f => f.Etapa == etapaEscolhida);
        }

        string? erroPeriodo = ValidacaoPeriodo.Validar(de, ate);
        if (erroPeriodo != null)
        {
            return Resultado<PaginaFalhasDto>.Invalido(erroPeriodo);
        }

        // As datas chegam como dia no horário de Brasília; no banco tudo é UTC
        if (de.HasValue)
        {
            DateTime inicioUtc = RelogioBrasil.InicioDoDiaEmUtc(de.Value);
            consulta = consulta.Where(f => f.OcorridaEm >= inicioUtc);
        }

        if (ate.HasValue)
        {
            DateTime fimUtc = RelogioBrasil.InicioDoDiaEmUtc(ate.Value.AddDays(1));
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
            resposta.Itens.Add(FalhaMapeamento.ParaDto(falha));
        }
        return Resultado<PaginaFalhasDto>.Ok(resposta);
    }
}
