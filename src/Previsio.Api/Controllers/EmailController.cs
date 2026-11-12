using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Email;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

[ApiController]
[Route("api/email")]
public class EmailController : PrevisioControllerBase
{
    private readonly TesteEmailService _teste;

    public EmailController(TesteEmailService teste)
    {
        _teste = teste;
    }

    /// <summary>
    /// Conecta e entra na caixa só para conferir os dados; não lê nem grava nada.
    /// Responde 200 com Conectou = false e o motivo quando o servidor recusa.
    /// </summary>
    [HttpPost("testar")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<ActionResult<ResultadoTesteEmailDto>> Testar(TesteEmailDto dados, CancellationToken cancelamento)
    {
        return Responder(await _teste.TestarAsync(dados, cancelamento));
    }
}
