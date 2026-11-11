using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Saude;

namespace Previsio.Api.Controllers;

/// <summary>
/// Saúde das integrações de todas as redes. Só administrador.
/// </summary>
[ApiController]
[Route("api/saude")]
[Authorize(Roles = Perfis.Admin)]
public class SaudeController : ControllerBase
{
    private readonly SaudeService _saude;

    public SaudeController(SaudeService saude)
    {
        _saude = saude;
    }

    [HttpGet]
    public Task<SaudeDto> Obter(CancellationToken cancelamento)
    {
        return _saude.ObterAsync(cancelamento);
    }
}
