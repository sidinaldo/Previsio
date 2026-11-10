namespace Previsio.Api.Dtos;

public class EmpresaDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Cnpj { get; set; } = "";
    public bool BlingConectado { get; set; }
    public DateTime? UltimaSincronizacaoEm { get; set; }
}
