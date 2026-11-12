using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Contas;
using Previsio.Api.Exportacao;
using Previsio.Api.Servicos;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Controllers;

/// <summary>
/// Planilhas Excel das listas do cliente. Os filtros são os mesmos das telas.
/// </summary>
[ApiController]
public class ExportacaoController : PrevisioControllerBase
{
    private readonly ExportacaoService _exportacao;

    public ExportacaoController(ExportacaoService exportacao)
    {
        _exportacao = exportacao;
    }

    /// <summary>
    /// Notas recebidas (ou pendentes, com situacao=Pendentes), uma linha por parcela.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/notas/exportar")]
    public async Task<IActionResult> Notas(
        int clienteId,
        [FromQuery] int? empresaId,
        [FromQuery] string? situacao,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? busca,
        [FromQuery] string? ordem,
        CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        Resultado<ArquivoExportado> resultado = await _exportacao.NotasAsync(
            clienteId, empresaId, situacao, de, ate, busca, ordem, cancelamento);
        return Responder(resultado, arquivo => File(arquivo.Conteudo, ArquivoExportado.TipoXlsx, arquivo.Nome));
    }

    /// <summary>
    /// Contas a pagar. Com "dias": as em aberto dos próximos N dias (as do painel).
    /// Sem "dias": os mesmos filtros da tela Contas a pagar.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/contas-pagar/exportar")]
    public async Task<IActionResult> ContasPagar(
        int clienteId,
        [FromQuery] int? dias,
        [FromQuery] int? empresaId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] string? situacao,
        [FromQuery] string? origem,
        [FromQuery] string? busca,
        [FromQuery] string? ordem,
        CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        Resultado<ArquivoExportado> resultado;
        if (dias.HasValue)
        {
            resultado = await _exportacao.ContasPagarAsync(clienteId, dias.Value, empresaId, cancelamento);
        }
        else
        {
            FiltroContas filtro = ContasPagarController.Filtro(empresaId, de, ate, situacao, origem, busca, ordem);
            resultado = await _exportacao.ContasPagarAsync(clienteId, filtro, cancelamento);
        }
        return Responder(resultado, arquivo => File(arquivo.Conteudo, ArquivoExportado.TipoXlsx, arquivo.Nome));
    }
}
