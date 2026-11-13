using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Sincronizacao;

namespace Previsio.Api.Controllers;

/// <summary>
/// Botão "Sincronizar com o Bling" das telas Contas a pagar e Contas a receber: sincroniza só a rede.
/// Aberto a todos os usuários da rede (e ao admin).
/// </summary>
[ApiController]
public class SincronizacaoController : PrevisioControllerBase
{
    private readonly SincronizacaoManualService _sincronizacao;

    public SincronizacaoController(SincronizacaoManualService sincronizacao)
    {
        _sincronizacao = sincronizacao;
    }

    /// <summary>
    /// Pede a sincronização das contas da rede. 409 quando ela sincronizou há menos de 2 minutos.
    /// </summary>
    [HttpPost("api/clientes/{clienteId:int}/sincronizacao")]
    public async Task<ActionResult<SituacaoSincronizacaoDto>> Sincronizar(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        Resultado<SituacaoSincronizacaoDto> resultado = await _sincronizacao.SolicitarAsync(clienteId, cancelamento);
        return Responder(resultado);
    }

    /// <summary>
    /// Se a sincronização ainda está na fila ou rodando, e quando a rede sincronizou pela última vez.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/sincronizacao")]
    public async Task<ActionResult<SituacaoSincronizacaoDto>> Situacao(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        return await _sincronizacao.SituacaoAsync(clienteId, cancelamento);
    }
}
