using MimeKit;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Infra.Email;
using Xunit;

namespace Previsio.Api.Tests.Jobs;

public class ConversaoEmailMimeKitTests
{
    [Fact]
    public void Converte_remetente_corpo_html_e_arquivos_inclusive_o_pdf_inline()
    {
        var mensagem = new MimeMessage();
        mensagem.MessageId = "abc@alfa.com.br";
        mensagem.From.Add(new MailboxAddress("Faturamento Alfa", "nf@alfa.com.br"));
        mensagem.Subject = "NF-e 48213";
        mensagem.Date = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(-3));
        var corpo = new Multipart("mixed");
        corpo.Add(new TextPart("html") { Text = "<p>Segue a <b>nota</b></p>" });
        var pdf = new MimePart("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(new byte[] { 37, 80, 68, 70 })),
            ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
            FileName = "danfe.pdf"
        };
        corpo.Add(pdf);
        mensagem.Body = corpo;

        EmailRecebido email = SessaoImapMailKit.Converter(42, mensagem);

        Assert.Equal(42, email.Uid);
        Assert.Equal("abc@alfa.com.br", email.MensagemId);
        Assert.Equal("nf@alfa.com.br", email.Remetente);
        Assert.Equal("Faturamento Alfa", email.NomeRemetente);
        Assert.Contains("nf@alfa.com.br", email.De);
        Assert.Equal("NF-e 48213", email.Assunto);
        Assert.Contains("Segue a  nota", email.Corpo);
        Assert.DoesNotContain("<p>", email.Corpo);
        Assert.Equal(new DateTime(2026, 11, 10, 12, 0, 0), email.EnviadoEm);
        ArquivoDoEmail arquivo = Assert.Single(email.Arquivos);
        Assert.Equal("danfe.pdf", arquivo.Nome);
        Assert.Equal("application/pdf", arquivo.Tipo);
        Assert.Equal(new byte[] { 37, 80, 68, 70 }, arquivo.Conteudo);
    }
}
