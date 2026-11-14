using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Vencimentos;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Controllers;

/// <summary>
/// Avisos de vencimento da rede: tolerância das vencidas e o e-mail diário. Admin e o próprio cliente.
/// </summary>
[ApiController]
[Route("api/clientes/{clienteId:int}/alertas-vencimento")]
public class AlertasVencimentoController : PrevisioControllerBase
{
    private readonly AlertaVencimentosService _alertas;

    public AlertasVencimentoController(AlertaVencimentosService alertas)
    {
        _alertas = alertas;
    }

    [HttpGet]
    public async Task<ActionResult<AlertasVencimentoDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _alertas.ObterAsync(clienteId, cancelamento));
    }

    [HttpPut]
    public async Task<ActionResult<AlertasVencimentoDto>> Alterar(int clienteId, AlertasVencimentoDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _alertas.AlterarAsync(clienteId, dados, cancelamento));
    }

    /// <summary>
    /// Envia o aviso de hoje agora, só para quem clicou. Não conta como o envio do dia.
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
        Resultado resultado = await _alertas.EnviarTesteAsync(clienteId, usuarioId.Value, cancelamento);
        return Responder(resultado, () => NoContent());
    }
}
