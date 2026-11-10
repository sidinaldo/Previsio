namespace Previsio.Api.Dtos;

/// <summary>
/// Só os campos preenchidos são alterados. O CNPJ não pode ser alterado.
/// </summary>
public class AlteracaoEmpresaDto
{
    public string? Nome { get; set; }
    public bool? Ativo { get; set; }

    // Aplicativo privado do Bling. Client ID vazio ("") remove o aplicativo próprio
    // e volta a usar o global. Secret vazio ou ausente mantém o atual.
    // Trocar o Client ID desconecta a empresa: os tokens pertencem ao aplicativo anterior.
    public string? BlingClientId { get; set; }
    public string? BlingClientSecret { get; set; }
}
