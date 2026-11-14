using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Exportacao;
using Previsio.Aplicacao.Relatorios;

namespace Previsio.Api.Controllers;

/// <summary>
/// Relatórios executivos: o Dossiê financeiro da rede (tela e planilha). O PDF é a impressão da tela.
/// </summary>
[ApiController]
public class RelatoriosController : PrevisioControllerBase
{
    private readonly DossieService _dossie;

    public RelatoriosController(DossieService dossie)
    {
        _dossie = dossie;
    }

    /// <summary>
    /// Projeção de 30, 60 ou 90 dias ("dias"), auditoria e comparativo do período (de/ate; sem eles, o mês atual).
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/dossie")]
    public async Task<ActionResult<DossieDto>> Obter(
        int clienteId, [FromQuery] int? dias, [FromQuery] DateTime? de, [FromQuery] DateTime? ate, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _dossie.MontarAsync(clienteId, dias ?? 30, de, ate, cancelamento));
    }

    /// <summary>Planilha do dossiê: uma aba por bloco e as contas a pagar do período.</summary>
    [HttpGet("api/clientes/{clienteId:int}/dossie/exportar")]
    public async Task<IActionResult> Exportar(
        int clienteId, [FromQuery] int? dias, [FromQuery] DateTime? de, [FromQuery] DateTime? ate, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        Resultado<ArquivoExportado> resultado = await _dossie.ExportarAsync(clienteId, dias ?? 30, de, ate, cancelamento);
        return Responder(resultado, arquivo => File(arquivo.Conteudo, ArquivoExportado.TipoXlsx, arquivo.Nome));
    }
}
