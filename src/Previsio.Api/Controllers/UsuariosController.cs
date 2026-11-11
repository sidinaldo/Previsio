using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Cadastro de usuários. Só administradores.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Perfis.Admin)]
public class UsuariosController : PrevisioControllerBase
{
    private readonly UsuariosService _usuarios;

    public UsuariosController(UsuariosService usuarios)
    {
        _usuarios = usuarios;
    }

    [HttpGet]
    public Task<List<UsuarioDto>> Listar([FromQuery] int? clienteId, CancellationToken cancelamento)
    {
        return _usuarios.ListarAsync(clienteId, cancelamento);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Criar(NovoUsuarioDto dados, CancellationToken cancelamento)
    {
        return Responder(await _usuarios.CriarAsync(dados, cancelamento));
    }

    /// <summary>
    /// Altera nome, ativa/desativa ou redefine a senha. Desativar ou redefinir a senha
    /// derruba na hora as sessões abertas do usuário.
    /// </summary>
    [HttpPut("{usuarioId:int}")]
    public async Task<ActionResult<UsuarioDto>> Alterar(
        int usuarioId, AlteracaoUsuarioDto dados, CancellationToken cancelamento)
    {
        return Responder(await _usuarios.AlterarAsync(usuarioId, dados, User.UsuarioId(), cancelamento));
    }
}
