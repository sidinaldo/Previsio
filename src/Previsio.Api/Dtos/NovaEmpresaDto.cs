namespace Previsio.Api.Dtos;

public class NovaEmpresaDto
{
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";

    // Opcionais: aplicativo privado criado na conta Bling desta empresa
    public string? BlingClientId { get; set; }
    public string? BlingClientSecret { get; set; }
}
