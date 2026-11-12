using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Controllers;

/// <summary>
/// Tela "Contas a pagar": as contas que a sincronização trouxe do Bling.
/// </summary>
[ApiController]
public class ContasPagarController : PrevisioControllerBase
{
    private readonly ContasPagarService _contas;

    public ContasPagarController(ContasPagarService contas)
    {
        _contas = contas;
    }

    /// <summary>
    /// Filtros: empresa, vencimento de/até, situação (Abertas, Vencidas, Pagas, Canceladas, Todas),
    /// origem (Previsio ou Bling) e busca por fornecedor, NF ou empresa.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/contas-pagar")]
    public async Task<ActionResult<PaginaContasPagarDto>> Listar(
        int clienteId,
        [FromQuery] int? empresaId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? situacao,
        [FromQuery] string? origem,
        [FromQuery] string? busca,
        [FromQuery] string? ordem,
        [FromQuery] int pagina,
        CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        FiltroContas filtro = Filtro(empresaId, de, ate, situacao, origem, busca, ordem);
        return Responder(await _contas.ListarAsync(clienteId, filtro, pagina, cancelamento));
    }

    public static FiltroContas Filtro(
        int? empresaId, DateTime? de, DateTime? ate, string? situacao, string? origem, string? busca, string? ordem)
    {
        var filtro = new FiltroContas();
        filtro.EmpresaId = empresaId;
        filtro.De = de;
        filtro.Ate = ate;
        filtro.Situacao = situacao;
        filtro.Origem = origem;
        filtro.Busca = busca;
        filtro.Ordem = ordem;
        return filtro;
    }
}
