namespace Previsio.Api.Autenticacao;

/// <summary>
/// Nomes das políticas de limite de requisições (rate limiting).
/// </summary>
public static class LimitesRequisicao
{
    // Tentativas de login por IP e por minuto: dificulta descobrir senhas por tentativa e erro.
    // O valor vem de Login:TentativasPorMinuto.
    public const string Login = "login";
    public const string ChaveTentativasLogin = "Login:TentativasPorMinuto";
    public const int TentativasLoginPadrao = 10;
}
