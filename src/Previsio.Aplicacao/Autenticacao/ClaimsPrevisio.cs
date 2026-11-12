namespace Previsio.Aplicacao.Autenticacao;

/// <summary>
/// Nomes das informações gravadas dentro do token.
/// </summary>
public static class ClaimsPrevisio
{
    public const string UsuarioId = "sub";
    public const string Nome = "name";
    public const string Email = "email";
    public const string Perfil = "role";
    public const string ClienteId = "cliente_id";
    public const string Carimbo = "carimbo";
    public const string IdToken = "jti";
}
