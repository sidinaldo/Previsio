using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Painel.Modelos;

namespace Previsio.Api.Controllers;

[ApiController]
public class PainelController : PrevisioControllerBase
{
    private readonly PainelService _painel;
    private readonly AcessoEmpresaService _acesso;

    public PainelController(PainelService painel, AcessoEmpresaService acesso)
    {
        _painel = painel;
        _acesso = acesso;
    }

    /// <summary>
    /// Visão consolidada: total dos próximos N dias, por semana e por empresa.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/painel")]
    public async Task<ActionResult<ResumoPainel>> Resumo(int clienteId, [FromQuery] int dias, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        return await _painel.ResumoAsync(clienteId, dias, cancelamento);
    }

    /// <summary>
    /// Vencimentos de uma empresa agrupados por dia.
    /// </summary>
    [HttpGet("api/empresas/{empresaId:int}/vencimentos")]
    public async Task<ActionResult<List<VencimentoDiaDto>>> Vencimentos(int empresaId, [FromQuery] int dias, CancellationToken cancelamento)
    {
        bool permitido = await _acesso.PodeAcessarAsync(User, empresaId, cancelamento);
        if (!permitido)
        {
            return NotFound(EmpresasService.MensagemNaoEncontrada);
        }

        return await _painel.VencimentosAsync(empresaId, dias, cancelamento);
    }
}
