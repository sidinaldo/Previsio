using MimeKit;

namespace Previsio.Infra.Email;

/// <summary>
/// Arquivos que vieram no e-mail. Além das partes marcadas como "anexo", conta as marcadas como
/// "inline" que são arquivo: muitos sistemas de faturamento, o Outlook e o Apple Mail mandam o PDF
/// ou o XML da nota assim, e o MimeKit não os lista em Attachments.
/// </summary>
public static class AnexosDoEmail
{
    public static List<MimePart> Listar(MimeMessage mensagem)
    {
        var arquivos = new List<MimePart>();
        foreach (MimeEntity entidade in mensagem.BodyParts)
        {
            MimePart? parte = entidade as MimePart;
            if (parte == null)
            {
                continue;
            }
            if (EhArquivo(parte))
            {
                arquivos.Add(parte);
            }
        }
        return arquivos;
    }

    private static bool EhArquivo(MimePart parte)
    {
        if (parte.IsAttachment)
        {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(parte.FileName))
        {
            // Imagem inline sem ser PDF/XML (logo da assinatura) não conta como arquivo do e-mail
            if (parte.ContentType.MediaType.Equals("image", StringComparison.OrdinalIgnoreCase)
                && parte.ContentDisposition != null
                && !parte.ContentDisposition.IsAttachment
                && !string.IsNullOrEmpty(parte.ContentId))
            {
                return false;
            }
            return true;
        }
        // Sem nome de arquivo: só PDF, XML ou ZIP pelo tipo (o corpo text/plain e text/html fica de fora)
        if (parte.ContentType.IsMimeType("application", "pdf")
            || parte.ContentType.IsMimeType("application", "xml")
            || parte.ContentType.IsMimeType("text", "xml")
            || parte.ContentType.IsMimeType("application", "zip")
            || parte.ContentType.IsMimeType("application", "x-zip-compressed"))
        {
            return true;
        }
        return false;
    }
}
