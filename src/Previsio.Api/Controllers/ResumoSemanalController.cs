using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Resumos;
using Previsio.Api.Servicos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Dia e hora do resumo semanal da rede. Admin altera qualquer rede; o usuário do cliente, a própria.
/// </summary>
[ApiController]
[Route("api/clientes/{clienteId:int}/resumo-semanal")]
public class ResumoSemanalController : PrevisioControllerBase
{
    private readonly ResumoSemanalService _resumos;

    public ResumoSemanalController(ResumoSemanalService resumos)
    {
        _resumos = resumos;
    }

    [HttpGet]
    public async Task<ActionResult<ResumoSemanalDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _resumos.ObterAsync(clienteId, cancelamento));
    }

    [HttpPut]
    public async Task<ActionResult<ResumoSemanalDto>> Alterar(
        int clienteId, AlteracaoResumoSemanalDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _resumos.AlterarAsync(clienteId, dados, cancelamento));
    }

    /// <summary>
    /// Envia o resumo agora, só para quem clicou. Não conta como o envio da semana.
    /// </summary>
    [HttpPost("teste")]
    public async Task<IActionResult> EnviarTeste(int clienteId, CancellationToken cancelamento)
    {
        int? usuarioId = User.UsuarioId();
        if (!usuarioId.HasValue)
        {
            return Unauthorized();
        }
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        Resultado resultado = await _resumos.EnviarTesteAsync(clienteId, usuarioId.Value, cancelamento);
        return Responder(resultado, () => NoContent());
    }
}
