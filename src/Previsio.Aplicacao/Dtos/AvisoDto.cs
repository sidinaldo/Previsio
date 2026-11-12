namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Um item do sininho.
/// </summary>
public class AvisoDto
{
    public int Id { get; set; }
    // NotaComProblema, BlingDesconectado ou CaixaEmailFalhando
    public string Tipo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Mensagem { get; set; } = "";
    // Tela do painel onde se resolve
    public string Link { get; set; } = "";
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = "";
    public DateTime CriadoEm { get; set; }
    // Criado depois da última vez que o usuário abriu o sininho
    public bool Novo { get; set; }
}
