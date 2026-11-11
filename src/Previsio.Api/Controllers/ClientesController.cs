using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;

namespace Previsio.Api.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : PrevisioControllerBase
{
    private readonly ClientesService _clientes;
    private readonly EmpresasService _empresas;

    public ClientesController(ClientesService clientes, EmpresasService empresas)
    {
        _clientes = clientes;
        _empresas = empresas;
    }

    [HttpGet]
    [Authorize(Roles = Perfis.Admin)]
    public Task<List<ClienteDto>> Listar(CancellationToken cancelamento)
    {
        return _clientes.ListarAsync(cancelamento);
    }

    /// <summary>
    /// Admin vê qualquer cliente; o usuário de cliente, só o próprio.
    /// </summary>
    [HttpGet("{clienteId:int}")]
    public async Task<ActionResult<ClienteDto>> Obter(int clienteId, CancellationToken cancelamento)
    {
        // Cliente de outro usuário responde igual a inexistente, para não revelar ids
        if (!User.PodeAcessarCliente(clienteId))
        {
            return NotFound(ClientesService.MensagemNaoEncontrado);
        }

        return Responder(await _clientes.ObterAsync(clienteId, cancelamento));
    }

    /// <summary>
    /// Altera os campos preenchidos. A senha do IMAP só troca se vier preenchida.
    /// Desativar o cliente derruba na hora as sessões dos usuários dele.
    /// </summary>
    [HttpPut("{clienteId:int}")]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<ActionResult<ClienteDto>> Alterar(
        int clienteId, AlteracaoClienteDto dados, CancellationToken cancelamento)
    {
        return Responder(await _clientes.AlterarAsync(clienteId, dados, cancelamento));
    }

    [HttpPost]
    [Authorize(Roles = Perfis.Admin)]
    public async Task<ActionResult<ClienteDto>> Criar(NovoClienteDto dados, CancellationToken cancelamento)
    {
        return Responder(await _clientes.CriarAsync(dados, cancelamento));
    }

    [HttpGet("{clienteId:int}/empresas")]
    public async Task<ActionResult<List<EmpresaDto>>> ListarEmpresas(int clienteId, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        return await _empresas.ListarDoClienteAsync(clienteId, cancelamento);
    }

    [HttpPost("{clienteId:int}/empresas")]
    public async Task<ActionResult<EmpresaDto>> CriarEmpresa(
        int clienteId, NovaEmpresaDto dados, CancellationToken cancelamento)
    {
        if (!User.PodeAcessarCliente(clienteId))
        {
            return Forbid();
        }

        return Responder(await _empresas.CriarAsync(clienteId, dados, cancelamento));
    }
}
