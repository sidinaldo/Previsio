using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Confere e-mail e senha e cuida do hash das senhas (PBKDF2 do ASP.NET Core Identity).
/// </summary>
public class AutenticacaoService
{
    // Hash de uma senha aleatória, conferido quando o e-mail não existe: assim a resposta
    // demora o mesmo tempo e não revela quais e-mails estão cadastrados.
    private static readonly string HashFicticio =
        new PasswordHasher<Usuario>().HashPassword(new Usuario(), Guid.NewGuid().ToString());

    private readonly PrevisioDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly ILogger<AutenticacaoService> _logger;

    public AutenticacaoService(
        PrevisioDbContext db,
        IPasswordHasher<Usuario> hasher,
        ILogger<AutenticacaoService> logger)
    {
        _db = db;
        _hasher = hasher;
        _logger = logger;
    }

    /// <summary>
    /// Retorna o usuário quando e-mail e senha conferem e o acesso está liberado.
    /// Qualquer falha retorna null, sem dizer o motivo para quem tentou entrar.
    /// </summary>
    public async Task<Usuario?> AutenticarAsync(string? email, string? senha, CancellationToken cancelamento)
    {
        string emailNormalizado = RegrasUsuario.NormalizarEmail(email);
        string senhaInformada = senha ?? "";

        if (emailNormalizado == "" || senhaInformada == "" || senhaInformada.Length > RegrasUsuario.TamanhoMaximoSenha)
        {
            return null;
        }

        Usuario? usuario = await _db.Usuarios
            .Include(u => u.Cliente)
            .FirstOrDefaultAsync(u => u.Email == emailNormalizado, cancelamento);

        if (usuario == null)
        {
            _hasher.VerifyHashedPassword(new Usuario(), HashFicticio, senhaInformada);
            _logger.LogWarning("Login recusado: e-mail {Email} não cadastrado", emailNormalizado);
            return null;
        }

        PasswordVerificationResult resultado = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, senhaInformada);
        if (resultado == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login recusado: senha incorreta para o usuário {UsuarioId}", usuario.Id);
            return null;
        }

        if (!usuario.Ativo || (usuario.Cliente != null && !usuario.Cliente.Ativo))
        {
            _logger.LogWarning("Login recusado: usuário {UsuarioId} ou cliente inativo", usuario.Id);
            return null;
        }

        // Hash gerado com parâmetros antigos: aproveita a senha em mãos para atualizar
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = _hasher.HashPassword(usuario, senhaInformada);
        }

        usuario.UltimoAcessoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancelamento);

        return usuario;
    }

    public bool SenhaConfere(Usuario usuario, string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length > RegrasUsuario.TamanhoMaximoSenha)
        {
            return false;
        }
        PasswordVerificationResult resultado = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, senha);
        return resultado != PasswordVerificationResult.Failed;
    }

    /// <summary>
    /// Grava a nova senha e invalida os tokens emitidos com a senha anterior.
    /// </summary>
    public void DefinirSenha(Usuario usuario, string senha)
    {
        usuario.SenhaHash = _hasher.HashPassword(usuario, senha);
        RevogarTokens(usuario);
    }

    public static void RevogarTokens(Usuario usuario)
    {
        usuario.CarimboSeguranca = Guid.NewGuid().ToString("N");
    }
}
