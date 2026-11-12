using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Servicos;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Controllers;

/// <summary>
/// Conexão OAuth de cada empresa com a sua conta Bling.
/// </summary>
[ApiController]
public class BlingController : PrevisioControllerBase
{
    private readonly BlingConexaoService _conexao;
    private readonly IConfiguration _configuracao;

    public BlingController(BlingConexaoService conexao, IConfiguration configuracao)
    {
        _conexao = conexao;
        _configuracao = configuracao;
    }

    /// <summary>
    /// Retorna a URL para o cliente autorizar o Previsio na conta Bling da empresa.
    /// </summary>
    [HttpGet("api/empresas/{empresaId:int}/bling/conectar")]
    public async Task<IActionResult> Conectar(int empresaId, CancellationToken cancelamento)
    {
        Resultado<string> resultado = await _conexao.MontarUrlConexaoAsync(empresaId, User, cancelamento);
        return Responder(resultado, url => Ok(new { url = url }));
    }

    /// <summary>
    /// Desconecta a empresa do Bling: apaga os tokens guardados.
    /// As notas dela passam a ficar como "Bling desconectado" até uma nova conexão.
    /// </summary>
    [HttpDelete("api/empresas/{empresaId:int}/bling")]
    public async Task<IActionResult> Desconectar(int empresaId, CancellationToken cancelamento)
    {
        Resultado resultado = await _conexao.DesconectarAsync(empresaId, User, cancelamento);
        return Responder(resultado, () => NoContent());
    }

    /// <summary>
    /// Link de redirecionamento cadastrado no app do Bling.
    /// Fica sem login porque quem chama é o navegador vindo do Bling; a empresa
    /// vem no state assinado, gerado só depois da verificação de acesso em Conectar.
    /// </summary>
    [HttpGet("api/bling/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, CancellationToken cancelamento)
    {
        string urlFront = _configuracao["Front:Url"] ?? "http://localhost:4200";
        string retorno = await _conexao.ConcluirConexaoAsync(code, state, cancelamento);
        return Redirect(urlFront + "/pages/empresas?bling=" + retorno);
    }
}
