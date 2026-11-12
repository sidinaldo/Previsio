using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Conferencia;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Controllers;

/// <summary>
/// Configuração da conferência com o Bling da rede. Admin e o próprio cliente.
/// </summary>
[ApiController]
[Route("api/clientes/{clienteId:int}/conferencia-bling")]
public class ConferenciaBlingController : PrevisioControllerBase
{
    private readonly ConfiguracaoConferenciaService _configuracao;

    public ConferenciaBlingController(ConfiguracaoConferenciaService configuracao)
    {
        _configuracao = configuracao;
    }

    [HttpGet]
    public async Task<ActionResult<ConferenciaBlingDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _configuracao.ObterAsync(clienteId, cancelamento));
    }

    [HttpPut]
    public async Task<ActionResult<ConferenciaBlingDto>> Alterar(int clienteId, ConferenciaBlingDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _configuracao.AlterarAsync(clienteId, dados, cancelamento));
    }
}
