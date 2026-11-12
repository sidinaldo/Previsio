using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Saude;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.EmailsIgnorados;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Saúde das integrações de todas as redes. Só administrador.
/// </summary>
[ApiController]
[Route("api/saude")]
[Authorize(Roles = Perfis.Admin)]
public class SaudeController : PrevisioControllerBase
{
    private readonly SaudeService _saude;
    private readonly EmailsIgnoradosService _ignorados;

    public SaudeController(SaudeService saude, EmailsIgnoradosService ignorados)
    {
        _saude = saude;
        _ignorados = ignorados;
    }

    [HttpGet]
    public Task<SaudeDto> Obter(CancellationToken cancelamento)
    {
        return _saude.ObterAsync(cancelamento);
    }

    /// <summary>
    /// E-mails com anexo que não viraram nota nos últimos N dias (1 a 30), de uma rede ou de todas.
    /// </summary>
    [HttpGet("emails-ignorados")]
    public Task<List<EmailIgnoradoDto>> EmailsIgnorados(
        [FromQuery] int? clienteId, [FromQuery] int dias, CancellationToken cancelamento)
    {
        return _ignorados.ListarAsync(clienteId, dias, cancelamento);
    }

    /// <summary>
    /// O PDF do e-mail ignorado, para conferir.
    /// </summary>
    [HttpGet("emails-ignorados/{id:int}/pdf")]
    public async Task<IActionResult> PdfIgnorado(int id, CancellationToken cancelamento)
    {
        EmailIgnorado? email = await _ignorados.ObterAsync(id, cancelamento);
        if (email == null || email.Pdf == null || email.Pdf.Length == 0)
        {
            return NotFound(EmailsIgnoradosService.MensagemNaoEncontrado);
        }
        string nome = email.PdfNome ?? "";
        if (nome == "")
        {
            nome = "email-" + email.Id + ".pdf";
        }
        return File(email.Pdf, "application/pdf", nome);
    }

    /// <summary>
    /// Trata o PDF do e-mail como nota: vira pendência em Notas pendentes.
    /// </summary>
    [HttpPost("emails-ignorados/{id:int}/tratar-como-nota")]
    public async Task<ActionResult<TratamentoEmailIgnoradoDto>> TratarComoNota(int id, CancellationToken cancelamento)
    {
        string usuario = User.Identity?.Name ?? "admin";
        return Responder(await _ignorados.TratarComoNotaAsync(id, usuario, cancelamento));
    }
}
