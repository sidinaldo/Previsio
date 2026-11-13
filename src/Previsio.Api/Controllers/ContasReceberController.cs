using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Tela "Contas a receber" e a configuração do fluxo de caixa da rede.
/// </summary>
[ApiController]
public class ContasReceberController : PrevisioControllerBase
{
    private readonly ContasReceberService _contas;
    private readonly FluxoCaixaService _fluxoCaixa;

    public ContasReceberController(ContasReceberService contas, FluxoCaixaService fluxoCaixa)
    {
        _contas = contas;
        _fluxoCaixa = fluxoCaixa;
    }

    /// <summary>
    /// Filtros: empresa, vencimento de/até, situação (Abertas, Vencidas, Recebidas, Canceladas, Todas)
    /// e busca por cliente ou empresa.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/contas-receber")]
    public async Task<ActionResult<PaginaContasReceberDto>> Listar(
        int clienteId,
        [FromQuery] int? empresaId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? situacao,
        [FromQuery] string? busca,
        [FromQuery] string? ordem,
        [FromQuery] int pagina,
        [FromQuery] int? tamanho,
        CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        FiltroContasReceber filtro = Filtro(empresaId, de, ate, situacao, busca, ordem);
        return Responder(await _contas.ListarAsync(clienteId, filtro, pagina, tamanho, cancelamento));
    }

    /// <summary>
    /// Fluxo de caixa da rede: ligado, a sincronização traz as contas a receber e o painel
    /// mostra entradas × saídas.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/fluxo-caixa")]
    public async Task<ActionResult<FluxoCaixaDto>> ObterFluxoCaixa(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _fluxoCaixa.ObterAsync(clienteId, cancelamento));
    }

    [HttpPut("api/clientes/{clienteId:int}/fluxo-caixa")]
    public async Task<ActionResult<FluxoCaixaDto>> AlterarFluxoCaixa(int clienteId, FluxoCaixaDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _fluxoCaixa.AlterarAsync(clienteId, dados, cancelamento));
    }

    public static FiltroContasReceber Filtro(
        int? empresaId, DateTime? de, DateTime? ate, string? situacao, string? busca, string? ordem)
    {
        var filtro = new FiltroContasReceber();
        filtro.EmpresaId = empresaId;
        filtro.De = de;
        filtro.Ate = ate;
        filtro.Situacao = situacao;
        filtro.Busca = busca;
        filtro.Ordem = ordem;
        return filtro;
    }
}
