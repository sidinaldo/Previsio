namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Só os campos preenchidos são alterados.
/// </summary>
public class AlteracaoUsuarioDto
{
    public string? Nome { get; set; }
    public bool? Ativo { get; set; }

    // Redefine a senha (ex.: usuário esqueceu). Derruba as sessões abertas dele.
    public string? NovaSenha { get; set; }
}
