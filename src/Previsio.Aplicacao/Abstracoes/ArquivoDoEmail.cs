namespace Previsio.Aplicacao.Abstracoes;

/// <summary>
/// Arquivo que veio no e-mail (anexo ou "inline"), já decodificado.
/// </summary>
public class ArquivoDoEmail
{
    // Nome do arquivo; vazio quando o e-mail não informa
    public string Nome { get; set; } = "";

    // Tipo do conteúdo em minúsculas, por exemplo "application/pdf"
    public string Tipo { get; set; } = "";

    public byte[] Conteudo { get; set; } = Array.Empty<byte>();
}
