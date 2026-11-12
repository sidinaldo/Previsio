using Microsoft.AspNetCore.Mvc;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.NotasSoPdf;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Api.Controllers;

/// <summary>
/// Notas que chegaram por e-mail só em PDF, sem o XML.
/// </summary>
[ApiController]
public class NotasSoPdfController : PrevisioControllerBase
{
    private readonly NotasSoPdfService _notas;

    public NotasSoPdfController(NotasSoPdfService notas)
    {
        _notas = notas;
    }

    [HttpGet("api/clientes/{clienteId:int}/notas-so-pdf")]
    public async Task<ActionResult<List<NotaSoPdfDto>>> Listar(int clienteId, [FromQuery] int? empresaId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }
        return await _notas.ListarAsync(clienteId, empresaId, cancelamento);
    }

    /// <summary>
    /// O PDF que chegou no e-mail, para conferir.
    /// </summary>
    [HttpGet("api/notas-so-pdf/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken cancelamento)
    {
        NotaSoPdf? nota = await _notas.ObterAsync(id, User, cancelamento);
        if (nota == null)
        {
            return NotFound(NotasSoPdfService.MensagemNaoEncontrada);
        }
        string nome = nota.PdfNome;
        if (nome == "")
        {
            nome = "nota-" + nota.Id + ".pdf";
        }
        return File(nota.Pdf, "application/pdf", nome);
    }

    [HttpPost("api/notas-so-pdf/{id:int}/xml")]
    public async Task<IActionResult> AnexarXml(int id, AnexarXmlDto dados, CancellationToken cancelamento)
    {
        return Responder(await _notas.AnexarXmlAsync(id, dados.Xml, User, cancelamento), () => NoContent());
    }

    [HttpPost("api/notas-so-pdf/{id:int}/chave")]
    public async Task<IActionResult> InformarChave(int id, InformarChaveDto dados, CancellationToken cancelamento)
    {
        return Responder(await _notas.InformarChaveAsync(id, dados.Chave, User, cancelamento), () => NoContent());
    }

    [HttpPost("api/notas-so-pdf/{id:int}/pedir-xml")]
    public async Task<IActionResult> PedirXml(int id, CancellationToken cancelamento)
    {
        return Responder(await _notas.PedirXmlAsync(id, User, cancelamento), () => NoContent());
    }

    [HttpPost("api/notas-so-pdf/{id:int}/lancada-manualmente")]
    public async Task<IActionResult> MarcarLancada(int id, ResolucaoManualDto dados, CancellationToken cancelamento)
    {
        return Responder(await _notas.MarcarLancadaAsync(id, dados.Observacao, User, cancelamento), () => NoContent());
    }
}
