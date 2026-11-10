namespace Previsio.Api.Dtos;

public class EmpresaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public bool BlingConectado { get; set; }

    // Aplicativo privado do Bling desta empresa. O secret nunca é devolvido,
    // só se está cadastrado. Client ID vazio: usa o aplicativo global do Previsio.
    public string? BlingClientId { get; set; }
    public bool BlingClientSecretCadastrado { get; set; }
    public DateTime? UltimaSincronizacaoEm { get; set; }
    public bool Ativo { get; set; }
}
