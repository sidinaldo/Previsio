using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Histórico das falhas ao ler notas do e-mail e ao lançá-las no Bling.
/// </summary>
[ApiController]
public class FalhasController : PrevisioControllerBase
{
    public const int TamanhoPagina = FalhasService.TamanhoPagina;

    private readonly FalhasService _falhas;

    public FalhasController(FalhasService falhas)
    {
        _falhas = falhas;
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

        return Responder(await _falhas.ListarAsync(clienteId, empresaId, notaId, etapa, de, ate, pagina, cancelamento));
    }
}
