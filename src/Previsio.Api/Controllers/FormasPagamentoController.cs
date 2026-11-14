using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.FormasPagamento;

namespace Previsio.Api.Controllers;

/// <summary>
/// Formas de pagamento da rede: forma padrão e regras "fornecedor → forma" para o lançamento.
/// </summary>
[ApiController]
public class FormasPagamentoController : PrevisioControllerBase
{
    private readonly FormasPagamentoService _formas;

    public FormasPagamentoController(FormasPagamentoService formas)
    {
        _formas = formas;
    }

    [HttpGet("api/clientes/{clienteId:int}/formas-pagamento")]
    public async Task<ActionResult<ConfiguracaoFormasPagamentoDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _formas.ObterAsync(clienteId, cancelamento));
    }

    [HttpPut("api/clientes/{clienteId:int}/formas-pagamento/padrao")]
    public async Task<ActionResult<ConfiguracaoFormasPagamentoDto>> AlterarPadrao(
        int clienteId, FormaPagamentoPadraoDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _formas.AlterarPadraoAsync(clienteId, dados, cancelamento));
    }

    /// <summary>Cria a regra do fornecedor ou troca a forma da que já existe.</summary>
    [HttpPost("api/clientes/{clienteId:int}/formas-pagamento/regras")]
    public async Task<ActionResult<ConfiguracaoFormasPagamentoDto>> SalvarRegra(
        int clienteId, NovaRegraFormaPagamentoDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _formas.SalvarRegraAsync(clienteId, dados, cancelamento));
    }

    [HttpDelete("api/clientes/{clienteId:int}/formas-pagamento/regras/{regraId:int}")]
    public async Task<ActionResult<ConfiguracaoFormasPagamentoDto>> RemoverRegra(
        int clienteId, int regraId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }
        return Responder(await _formas.RemoverRegraAsync(clienteId, regraId, cancelamento));
    }
}
