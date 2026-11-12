using MimeKit;
using Previsio.Api.Jobs;
using Xunit;
using Previsio.Infra.Email;

namespace Previsio.Api.Tests.Jobs;

public class AnexosDoEmailTests
{
    private static MimePart Parte(string tipo, string subtipo, string? nome, string disposicao, string? contentId = null)
    {
        var parte = new MimePart(tipo, subtipo);
        parte.Content = new MimeContent(new MemoryStream(new byte[] { 1, 2, 3 }));
        parte.ContentDisposition = new ContentDisposition(disposicao);
        if (nome != null)
        {
            parte.FileName = nome;
        }
        if (contentId != null)
        {
            parte.ContentId = contentId;
        }
        return parte;
    }

    [Fact]
    public void Conta_pdf_e_xml_inline_e_deixa_de_fora_o_corpo_e_o_logo_da_assinatura()
    {
        var mensagem = new MimeMessage();
        var corpo = new Multipart("mixed");
        corpo.Add(new TextPart("plain") { Text = "Segue a nota" });
        corpo.Add(new TextPart("html") { Text = "<p>Segue a nota</p>" });
        corpo.Add(Parte("application", "pdf", "danfe.pdf", ContentDisposition.Inline));
        corpo.Add(Parte("application", "xml", null, ContentDisposition.Inline));
        corpo.Add(Parte("image", "png", "logo.png", ContentDisposition.Inline, "logo@assinatura"));
        corpo.Add(Parte("application", "octet-stream", "planilha.xlsx", ContentDisposition.Attachment));
        mensagem.Body = corpo;

        List<string> tipos = AnexosDoEmail.Listar(mensagem).Select(p => p.ContentType.MimeType + "|" + (p.FileName ?? "")).ToList();

        Assert.Equal(new List<string> { "application/pdf|danfe.pdf", "application/xml|", "application/octet-stream|planilha.xlsx" }, tipos);
        // O MimeKit sozinho só veria a planilha
        Assert.Single(mensagem.Attachments);
    }
}
