using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Hash das senhas dos usuários (implementado na Infra com o PBKDF2 do ASP.NET Core Identity).
/// </summary>
public interface IHashSenha
{
    string Gerar(Usuario usuario, string senha);

    ConferenciaSenha Conferir(Usuario usuario, string hash, string senha);
}
