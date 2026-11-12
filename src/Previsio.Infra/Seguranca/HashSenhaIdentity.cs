using Microsoft.AspNetCore.Identity;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Infra.Seguranca;

/// <summary>
/// Hash de senha com o PasswordHasher do ASP.NET Core Identity (PBKDF2). As senhas já gravadas continuam valendo.
/// </summary>
public class HashSenhaIdentity : IHashSenha
{
    private readonly PasswordHasher<Usuario> _hasher = new PasswordHasher<Usuario>();

    public string Gerar(Usuario usuario, string senha)
    {
        return _hasher.HashPassword(usuario, senha);
    }

    public ConferenciaSenha Conferir(Usuario usuario, string hash, string senha)
    {
        PasswordVerificationResult resultado = _hasher.VerifyHashedPassword(usuario, hash, senha);
        if (resultado == PasswordVerificationResult.Success)
        {
            return ConferenciaSenha.Correta;
        }
        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            return ConferenciaSenha.CorretaPrecisaRenovar;
        }
        return ConferenciaSenha.Incorreta;
    }
}
