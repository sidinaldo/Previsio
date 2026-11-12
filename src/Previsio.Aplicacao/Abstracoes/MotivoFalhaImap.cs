namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Por que não foi possível entrar na caixa de e-mail (para mostrar uma mensagem que ajude).
/// </summary>
public enum MotivoFalhaImap
{
    Outro = 0,

    // Usuário ou senha recusados
    Autenticacao = 1,

    // A conexão segura (SSL/TLS) não fechou
    Ssl = 2,

    // Servidor ou porta inacessível
    Rede = 3
}
