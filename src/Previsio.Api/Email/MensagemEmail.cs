namespace Previsio.Api.Email;

/// <summary>
/// Um e-mail a enviar, com a versão HTML e a de texto puro (para leitores que não mostram HTML).
/// </summary>
public class MensagemEmail
{
    public string Para { get; set; } = "";
    public string NomePara { get; set; } = "";
    public string Assunto { get; set; } = "";
    public string Html { get; set; } = "";
    public string Texto { get; set; } = "";
}
