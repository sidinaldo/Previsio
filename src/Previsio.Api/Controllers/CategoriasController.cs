using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Categorias;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Categorias da rede: categoria padrão e regras "fornecedor → categoria" para o lançamento.
/// </summary>
[ApiController]
public class CategoriasController : PrevisioControllerBase
{
    private readonly CategoriasService _categorias;

    public CategoriasController(CategoriasService categorias)
    {
        _categorias = categorias;
    }

    [HttpGet("api/clientes/{clienteId:int}/categorias")]
    public async Task<ActionResult<ConfiguracaoCategoriasDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _categorias.ObterAsync(clienteId, cancelamento));
    }

    [HttpPut("api/clientes/{clienteId:int}/categorias/padrao")]
    public async Task<ActionResult<ConfiguracaoCategoriasDto>> AlterarPadrao(
        int clienteId, CategoriaPadraoDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _categorias.AlterarPadraoAsync(clienteId, dados, cancelamento));
    }

    /// <summary>Cria a regra do fornecedor ou troca a categoria da que já existe.</summary>
    [HttpPost("api/clientes/{clienteId:int}/categorias/regras")]
    public async Task<ActionResult<ConfiguracaoCategoriasDto>> SalvarRegra(
        int clienteId, NovaRegraCategoriaDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _categorias.SalvarRegraAsync(clienteId, dados, cancelamento));
    }

    [HttpDelete("api/clientes/{clienteId:int}/categorias/regras/{regraId:int}")]
    public async Task<ActionResult<ConfiguracaoCategoriasDto>> RemoverRegra(
        int clienteId, int regraId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _categorias.RemoverRegraAsync(clienteId, regraId, cancelamento));
    }
}
