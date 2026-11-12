using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Servicos;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Controllers;

/// <summary>
/// Base dos controllers: traduz o resultado dos serviços em resposta HTTP.
/// </summary>
public abstract class PrevisioControllerBase : ControllerBase
{
    protected ActionResult<T> Responder<T>(Resultado<T> resultado)
    {
        if (resultado.Sucesso)
        {
            return new ActionResult<T>(resultado.Valor!);
        }
        return RespostaDeErro(resultado.Tipo, resultado.Mensagem);
    }

    protected IActionResult Responder(Resultado resultado, Func<IActionResult> seDerCerto)
    {
        if (resultado.Sucesso)
        {
            return seDerCerto();
        }
        return RespostaDeErro(resultado.Tipo, resultado.Mensagem);
    }

    protected IActionResult Responder<T>(Resultado<T> resultado, Func<T, IActionResult> seDerCerto)
    {
        if (resultado.Sucesso)
        {
            return seDerCerto(resultado.Valor!);
        }
        return RespostaDeErro(resultado.Tipo, resultado.Mensagem);
    }

    private ActionResult RespostaDeErro(TipoResultado tipo, string? mensagem)
    {
        if (tipo == TipoResultado.NaoEncontrado)
        {
            if (mensagem == null)
            {
                return NotFound();
            }
            return NotFound(mensagem);
        }
        if (tipo == TipoResultado.Conflito)
        {
            return Conflict(mensagem);
        }
        return BadRequest(mensagem);
    }
}
