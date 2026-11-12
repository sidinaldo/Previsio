namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Resultado da conferência de uma senha com o hash gravado.
/// </summary>
public enum ConferenciaSenha
{
    Incorreta = 0,
    Correta = 1,

    // Correta, mas o hash foi gerado com parâmetros antigos: convém gravar um novo
    CorretaPrecisaRenovar = 2
}
