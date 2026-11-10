using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Controllers;

/// <summary>
/// Cadastro de usuários. Só administradores.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Perfis.Admin)]
public class UsuariosController : ControllerBase
{
    private const int TamanhoMaximoNome = 200;

    private readonly PrevisioDbContext _db;
    private readonly AutenticacaoService _autenticacao;

    public UsuariosController(PrevisioDbContext db, AutenticacaoService autenticacao)
    {
        _db = db;
        _autenticacao = autenticacao;
    }

    [HttpGet]
    public async Task<List<UsuarioDto>> Listar([FromQuery] int? clienteId, CancellationToken cancelamento)
    {
        IQueryable<Usuario> consulta = _db.Usuarios;
        if (clienteId.HasValue)
        {
            consulta = consulta.Where(u => u.ClienteId == clienteId.Value);
        }

        List<Usuario> usuarios = await consulta
            .OrderBy(u => u.Nome)
            .ToListAsync(cancelamento);

        var lista = new List<UsuarioDto>();
        foreach (Usuario usuario in usuarios)
        {
            lista.Add(ParaDto(usuario));
        }
        return lista;
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Criar(NovoUsuarioDto dados, CancellationToken cancelamento)
    {
        string nome = dados.Nome.Trim();
        if (nome == "" || nome.Length > TamanhoMaximoNome)
        {
            return BadRequest("Informe o nome do usuário (até " + TamanhoMaximoNome + " caracteres).");
        }

        string email = RegrasUsuario.NormalizarEmail(dados.Email);
        if (!RegrasUsuario.EmailValido(email))
        {
            return BadRequest("E-mail inválido.");
        }

        string? erroSenha = RegrasUsuario.ValidarSenha(dados.Senha);
        if (erroSenha != null)
        {
            return BadRequest(erroSenha);
        }

        PerfilUsuario perfil;
        if (!Perfis.TentarLer(dados.Perfil, out perfil))
        {
            return BadRequest("Perfil deve ser " + Perfis.Cliente + " ou " + Perfis.Admin + ".");
        }

        if (perfil == PerfilUsuario.Admin)
        {
            if (dados.ClienteId.HasValue)
            {
                return BadRequest("Administrador não pertence a um cliente: deixe clienteId vazio.");
            }
        }
        else
        {
            if (!dados.ClienteId.HasValue)
            {
                return BadRequest("Informe o clienteId do usuário.");
            }

            bool clienteExiste = await _db.Clientes.AnyAsync(c => c.Id == dados.ClienteId.Value, cancelamento);
            if (!clienteExiste)
            {
                return NotFound("Cliente não encontrado.");
            }
        }

        bool emailEmUso = await _db.Usuarios.AnyAsync(u => u.Email == email, cancelamento);
        if (emailEmUso)
        {
            return Conflict("Já existe um usuário com esse e-mail.");
        }

        var usuario = new Usuario();
        usuario.Nome = nome;
        usuario.Email = email;
        usuario.Perfil = perfil;
        usuario.ClienteId = dados.ClienteId;
        _autenticacao.DefinirSenha(usuario, dados.Senha);

        _db.Usuarios.Add(usuario);

        try
        {
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (DbUpdateException)
        {
            // Outro cadastro gravou o mesmo e-mail ao mesmo tempo (índice único)
            _db.ChangeTracker.Clear();
            return Conflict("Já existe um usuário com esse e-mail.");
        }

        return ParaDto(usuario);
    }

    /// <summary>
    /// Altera nome, ativa/desativa ou redefine a senha. Desativar ou redefinir a senha
    /// derruba na hora as sessões abertas do usuário.
    /// </summary>
    [HttpPut("{usuarioId:int}")]
    public async Task<ActionResult<UsuarioDto>> Alterar(
        int usuarioId, AlteracaoUsuarioDto dados, CancellationToken cancelamento)
    {
        Usuario? usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
        if (usuario == null)
        {
            return NotFound("Usuário não encontrado.");
        }

        if (dados.Nome != null)
        {
            string nome = dados.Nome.Trim();
            if (nome == "" || nome.Length > TamanhoMaximoNome)
            {
                return BadRequest("Informe o nome do usuário (até " + TamanhoMaximoNome + " caracteres).");
            }
            usuario.Nome = nome;
        }

        if (dados.NovaSenha != null)
        {
            string? erroSenha = RegrasUsuario.ValidarSenha(dados.NovaSenha);
            if (erroSenha != null)
            {
                return BadRequest(erroSenha);
            }
            _autenticacao.DefinirSenha(usuario, dados.NovaSenha);
        }

        if (dados.Ativo.HasValue && dados.Ativo.Value != usuario.Ativo)
        {
            if (!dados.Ativo.Value && User.UsuarioId() == usuario.Id)
            {
                return BadRequest("Você não pode desativar o próprio usuário.");
            }

            usuario.Ativo = dados.Ativo.Value;
            if (!usuario.Ativo)
            {
                AutenticacaoService.RevogarTokens(usuario);
            }
        }

        await _db.SaveChangesAsync(cancelamento);
        return ParaDto(usuario);
    }

    public static UsuarioDto ParaDto(Usuario usuario)
    {
        var dto = new UsuarioDto();
        dto.Id = usuario.Id;
        dto.ClienteId = usuario.ClienteId;
        dto.Nome = usuario.Nome;
        dto.Email = usuario.Email;
        dto.Perfil = Perfis.Nome(usuario.Perfil);
        dto.Ativo = usuario.Ativo;
        dto.UltimoAcessoEm = usuario.UltimoAcessoEm;
        return dto;
    }
}
