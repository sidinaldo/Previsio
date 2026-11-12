namespace Previsio.Aplicacao.Dtos;

public class NovoClienteDto
{
    public string Nome { get; set; } = "";
    public string ImapHost { get; set; } = "";
    public int ImapPorta { get; set; } = 993;
    public bool ImapSsl { get; set; } = true;
    public string ImapUsuario { get; set; } = "";
    public string ImapSenha { get; set; } = "";

    // Opcionais: aplicativo privado do Bling da rede, usado pelas empresas que não têm um próprio
    public string? BlingClientId { get; set; }
    public string? BlingClientSecret { get; set; }

    // Começa no teste grátis de 15 dias (falso: já contratado)
    public bool EmTeste { get; set; }
}
