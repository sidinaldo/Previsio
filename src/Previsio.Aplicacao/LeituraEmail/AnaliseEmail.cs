using System.IO.Compression;
using System.Text;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.EmailsIgnorados;
using Previsio.Aplicacao.NotasSoPdf;

namespace Previsio.Aplicacao.LeituraEmail;

/// <summary>
/// O que interessa num e-mail lido: os XMLs de NF-e (soltos ou dentro de .zip), os PDFs, a origem
/// e os dados para a lista de e-mails ignorados.
/// </summary>
public static class AnaliseEmail
{
    public const string MotivoAnexoSemNota = "Os anexos não são XML, ZIP com XML nem PDF.";

    // Proteção contra arquivos gigantes dentro do zip
    private const long TamanhoMaximoXmlNoZip = 5 * 1024 * 1024;

    public static string Origem(EmailRecebido email)
    {
        return email.De + " | " + email.Assunto;
    }

    public static List<AnexoXml> ExtrairXmls(EmailRecebido email)
    {
        var xmls = new List<AnexoXml>();
        foreach (ArquivoDoEmail arquivo in email.Arquivos)
        {
            string nomeArquivo = arquivo.Nome.ToLowerInvariant();
            bool tipoXml = arquivo.Tipo == "application/xml" || arquivo.Tipo == "text/xml";
            bool tipoZip = arquivo.Tipo == "application/zip" || arquivo.Tipo == "application/x-zip-compressed";
            if (nomeArquivo.EndsWith(".xml") || (nomeArquivo == "" && tipoXml))
            {
                xmls.Add(NovoAnexo(arquivo.Nome, arquivo.Conteudo));
            }
            else if (nomeArquivo.EndsWith(".zip") || (nomeArquivo == "" && tipoZip))
            {
                xmls.AddRange(ExtrairXmlsDoZip(arquivo.Nome, arquivo.Conteudo));
            }
        }
        return xmls;
    }

    /// <summary>
    /// Os PDFs do e-mail e o que o e-mail diz (remetente, assunto, corpo); nulo se não houver PDF.
    /// </summary>
    public static EmailSemXml? ComPdf(EmailRecebido email)
    {
        var semXml = new EmailSemXml();
        foreach (ArquivoDoEmail arquivo in email.Arquivos)
        {
            bool ehPdf = arquivo.Nome.ToLowerInvariant().EndsWith(".pdf") || arquivo.Tipo == "application/pdf";
            if (!ehPdf)
            {
                continue;
            }
            var pdf = new PdfDoEmail();
            pdf.Nome = arquivo.Nome;
            pdf.Conteudo = arquivo.Conteudo;
            semXml.Pdfs.Add(pdf);
        }
        if (semXml.Pdfs.Count == 0)
        {
            return null;
        }

        semXml.Remetente = email.Remetente;
        semXml.NomeRemetente = email.NomeRemetente;
        semXml.MensagemId = email.MensagemId;
        semXml.Assunto = email.Assunto;
        semXml.Origem = Origem(email);
        semXml.Corpo = email.Corpo;
        return semXml;
    }

    /// <summary>
    /// Dados do e-mail ignorado para a lista da tela Saúde.
    /// </summary>
    public static EmailParaRegistrar ParaRegistrar(
        EmailRecebido email, int clienteId, int? empresaCaixaId, string motivo, PdfDoEmail? pdf)
    {
        var registro = new EmailParaRegistrar();
        registro.ClienteId = clienteId;
        registro.EmpresaCaixaId = empresaCaixaId;
        registro.Uid = email.Uid;
        registro.MensagemId = email.MensagemId;
        registro.Remetente = email.Remetente;
        registro.NomeRemetente = email.NomeRemetente;
        registro.Assunto = email.Assunto;
        registro.Corpo = email.Corpo;
        registro.EnviadoEm = email.EnviadoEm;
        foreach (ArquivoDoEmail arquivo in email.Arquivos)
        {
            string nome = arquivo.Nome;
            if (nome == "")
            {
                nome = "(sem nome, " + arquivo.Tipo + ")";
            }
            registro.Anexos.Add(nome);
        }
        registro.Motivo = motivo;
        if (pdf != null)
        {
            registro.PdfNome = pdf.Nome;
            registro.Pdf = pdf.Conteudo;
        }
        return registro;
    }

    private static List<AnexoXml> ExtrairXmlsDoZip(string nomeZip, byte[] bytes)
    {
        var xmls = new List<AnexoXml>();
        try
        {
            using var memoria = new MemoryStream(bytes);
            using var zip = new ZipArchive(memoria, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entrada in zip.Entries)
            {
                if (!entrada.Name.ToLowerInvariant().EndsWith(".xml"))
                {
                    continue;
                }
                if (entrada.Length > TamanhoMaximoXmlNoZip)
                {
                    continue;
                }

                using Stream fluxo = entrada.Open();
                using var destino = new MemoryStream();
                fluxo.CopyTo(destino);
                xmls.Add(NovoAnexo(nomeZip + "/" + entrada.FullName, destino.ToArray()));
            }
        }
        catch (InvalidDataException)
        {
            // Zip corrompido: ignora
        }
        return xmls;
    }

    private static AnexoXml NovoAnexo(string nome, byte[] bytes)
    {
        var anexo = new AnexoXml();
        anexo.Nome = nome;
        // XML de NF-e é UTF-8; remove BOM se houver
        anexo.Conteudo = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        return anexo;
    }
}
