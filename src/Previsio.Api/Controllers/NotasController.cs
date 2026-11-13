using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Notas;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Controllers;

[ApiController]
public class NotasController : PrevisioControllerBase
{
    // Filtro de situação que junta todas as que ainda não viraram conta a pagar
    public const string SituacaoPendentes = NotasService.SituacaoPendentes;

    private readonly NotasService _notas;

    public NotasController(NotasService notas)
    {
        _notas = notas;
    }

    /// <summary>
    /// Notas que chegaram no e-mail e ainda não viraram conta a pagar no Bling.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/notas/pendentes")]
    public async Task<ActionResult<List<NotaDto>>> NotasPendentes(int clienteId, [FromQuery] int? empresaId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        return await _notas.ListarPendentesAsync(clienteId, empresaId, cancelamento);
    }

    /// <summary>
    /// Quantidade de notas pendentes, para o número do menu lateral.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/notas/pendentes/total")]
    public async Task<ActionResult<int>> TotalPendentes(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        return await _notas.ContarPendentesAsync(clienteId, cancelamento);
    }

    /// <summary>
    /// Todas as notas lidas do e-mail (lançadas ou não), da recebida mais recente para a mais antiga, com as parcelas.
    /// Filtros opcionais: empresa, situação (nome da situação ou "Pendentes") e período em que a nota
    /// chegou (datas no formato yyyy-MM-dd, horário de Brasília). "busca" procura por NF, fornecedor,
    /// empresa ou CNPJ; "ordem" é a coluna, com "-" na frente para decrescente.
    /// </summary>
    [HttpGet("api/clientes/{clienteId:int}/notas")]
    public async Task<ActionResult<PaginaNotasDto>> Listar(
        int clienteId,
        [FromQuery] int? empresaId,
        [FromQuery] string? situacao,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
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

        return Responder(await _notas.ListarAsync(clienteId, empresaId, situacao, de, ate, busca, ordem, pagina, tamanho, cancelamento));
    }

    /// <summary>
    /// Marca a nota com problema como resolvida e lançada manualmente no Bling.
    /// </summary>
    [HttpPost("api/notas/{notaId:int}/lancada-manualmente")]
    public async Task<IActionResult> MarcarLancadaManualmente(
        int notaId, ResolucaoManualDto dados, CancellationToken cancelamento)
    {
        Resultado resultado = await _notas.MarcarLancadaManualmenteAsync(notaId, dados.Observacao, User, cancelamento);
        return Responder(resultado, () => NoContent());
    }

    [HttpPost("api/notas/{notaId:int}/reprocessar")]
    public async Task<IActionResult> Reprocessar(int notaId, CancellationToken cancelamento)
    {
        Resultado resultado = await _notas.ReprocessarAsync(notaId, User, cancelamento);
        return Responder(resultado, () => Accepted());
    }
}
