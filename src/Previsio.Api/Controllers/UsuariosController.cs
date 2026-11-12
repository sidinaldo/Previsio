using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Aplicacao.Usuarios;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

/// <summary>
/// Cadastro de usuários. O admin cuida de todos; o gestor, só dos usuários da própria rede.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Perfis.GestaoUsuarios)]
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
        return _usuarios.ListarAsync(User, clienteId, cancelamento);
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Criar(NovoUsuarioDto dados, CancellationToken cancelamento)
    {
        return Responder(await _usuarios.CriarAsync(User, dados, cancelamento));
    }

    /// <summary>
    /// Altera nome, e-mail, perfil (Cliente ou Gestor), ativa/desativa ou redefine a senha.
    /// Desativar, redefinir a senha ou mudar o perfil derruba na hora as sessões abertas do usuário.
    /// </summary>
    [HttpPut("{usuarioId:int}")]
    public async Task<ActionResult<UsuarioDto>> Alterar(
        int usuarioId, AlteracaoUsuarioDto dados, CancellationToken cancelamento)
    {
        return Responder(await _usuarios.AlterarAsync(User, usuarioId, dados, cancelamento));
    }
}
