using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Sininho: o que precisa de ação nas redes que o usuário enxerga.
/// </summary>
[ApiController]
[Route("api/avisos")]
public class AvisosController : PrevisioControllerBase
{
    private readonly AvisosService _avisos;

    public AvisosController(AvisosService avisos)
    {
        _avisos = avisos;
    }

    [HttpGet]
    public async Task<ActionResult<PainelAvisosDto>> Listar(CancellationToken cancelamento)
    {
        int? usuarioId = User.UsuarioId();
        if (!usuarioId.HasValue)
        {
            return Unauthorized();
        }

        PainelAvisosDto? painel = await _avisos.PainelAsync(usuarioId.Value, cancelamento);
        if (painel == null)
        {
            return Unauthorized();
        }
        return painel;
    }

    /// <summary>
    /// O usuário abriu o sininho: zera o número de novos.
    /// </summary>
    [HttpPost("vistos")]
    public async Task<IActionResult> MarcarVistos(CancellationToken cancelamento)
    {
        int? usuarioId = User.UsuarioId();
        if (!usuarioId.HasValue || !await _avisos.MarcarVistosAsync(usuarioId.Value, cancelamento))
        {
            return Unauthorized();
        }
        return NoContent();
    }
}
