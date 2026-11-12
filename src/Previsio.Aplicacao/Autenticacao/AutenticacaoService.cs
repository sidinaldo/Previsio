using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Aplicacao.Autenticacao;

/// <summary>
/// Confere e-mail e senha e cuida do hash das senhas (IHashSenha).
/// </summary>
public class AutenticacaoService
{
    private readonly IRepositorio<Usuario> _usuarios;
    private readonly IHashSenha _hasher;

    // Hash de uma senha aleatória, conferido quando o e-mail não existe: assim a resposta
    // demora o mesmo tempo e não revela quais e-mails estão cadastrados.
    private readonly string _hashFicticio;
    private readonly ILogger<AutenticacaoService> _logger;

    public AutenticacaoService(
        IRepositorio<Usuario> usuarios,
        IHashSenha hasher,
        ILogger<AutenticacaoService> logger)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _hashFicticio = hasher.Gerar(new Usuario(), Guid.NewGuid().ToString());
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

        Usuario? usuario = await _usuarios.Consultar()
            .Include(u => u.Cliente)
            .FirstOrDefaultAsync(u => u.Email == emailNormalizado, cancelamento);

        if (usuario == null)
        {
            _hasher.Conferir(new Usuario(), _hashFicticio, senhaInformada);
            _logger.LogWarning("Login recusado: e-mail {Email} não cadastrado", emailNormalizado);
            return null;
        }

        ConferenciaSenha resultado = _hasher.Conferir(usuario, usuario.SenhaHash, senhaInformada);
        if (resultado == ConferenciaSenha.Incorreta)
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
        if (resultado == ConferenciaSenha.CorretaPrecisaRenovar)
        {
            usuario.SenhaHash = _hasher.Gerar(usuario, senhaInformada);
        }

        usuario.UltimoAcessoEm = DateTime.UtcNow;
        await _usuarios.SalvarAsync(cancelamento);

        return usuario;
    }

    public async Task<Usuario?> ObterUsuarioAsync(int usuarioId, CancellationToken cancelamento)
    {
        return await _usuarios.Consultar().FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
    }

    /// <summary>
    /// Troca a senha do próprio usuário, conferindo a senha atual.
    /// Os outros tokens dele deixam de valer.
    /// </summary>
    public async Task<Resultado> TrocarSenhaAsync(
        Usuario usuario, string? senhaAtual, string? novaSenha, CancellationToken cancelamento)
    {
        if (!SenhaConfere(usuario, senhaAtual))
        {
            return Resultado.Invalido("Senha atual incorreta.");
        }

        string? erroSenha = RegrasUsuario.ValidarSenha(novaSenha);
        if (erroSenha != null)
        {
            return Resultado.Invalido(erroSenha);
        }

        DefinirSenha(usuario, novaSenha!);
        await _usuarios.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }

    public bool SenhaConfere(Usuario usuario, string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length > RegrasUsuario.TamanhoMaximoSenha)
        {
            return false;
        }
        ConferenciaSenha resultado = _hasher.Conferir(usuario, usuario.SenhaHash, senha);
        return resultado != ConferenciaSenha.Incorreta;
    }

    /// <summary>
    /// Grava a nova senha e invalida os tokens emitidos com a senha anterior.
    /// </summary>
    public void DefinirSenha(Usuario usuario, string senha)
    {
        usuario.SenhaHash = _hasher.Gerar(usuario, senha);
        RevogarTokens(usuario);
    }

    public static void RevogarTokens(Usuario usuario)
    {
        usuario.CarimboSeguranca = Guid.NewGuid().ToString("N");
    }
}
