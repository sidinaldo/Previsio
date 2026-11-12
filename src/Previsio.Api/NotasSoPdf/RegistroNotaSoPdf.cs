using Microsoft.EntityFrameworkCore;
using Previsio.Api.Sefaz;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// Registra a nota que chegou só em PDF: procura a chave de acesso (nome do arquivo, assunto,
/// corpo do e-mail e texto do PDF) e deixa a nota na fila da SEFAZ ou pendente, sem chave.
/// PDF que não parece nota fiscal (boleto, pedido) é ignorado.
/// </summary>
public class RegistroNotaSoPdf
{
    public const int TamanhoMaximoPdf = 10 * 1024 * 1024;
    public const string MotivoSemChave = "Não achei a chave de acesso no PDF nem no e-mail. Informe a chave, anexe o XML ou peça o XML ao fornecedor.";

    private static readonly string[] SinaisDeNota = new string[]
    {
        "DANFE", "DOCUMENTO AUXILIAR DA NOTA FISCAL", "NOTA FISCAL ELETR", "NF-E", "NFE"
    };

    private readonly PrevisioDbContext _db;

    public RegistroNotaSoPdf(PrevisioDbContext db)
    {
        _db = db;
    }

    /// <returns>Quantas notas em PDF ficaram registradas.</returns>
    public async Task<int> RegistrarAsync(int clienteId, int? empresaDaCaixaId, EmailSemXml email, CancellationToken cancelamento)
    {
        if (email.Pdfs.Count == 0)
        {
            return 0;
        }

        // A mesma mensagem lida de novo (falha no meio da leitura) não duplica
        if (email.MensagemId != "")
        {
            bool jaRegistrado = await _db.NotasSoPdf.AnyAsync(
                n => n.ClienteId == clienteId && n.MensagemId == email.MensagemId, cancelamento);
            if (jaRegistrado)
            {
                return 0;
            }
        }

        // Uma pendência por chave achada; os PDFs sem chave só contam se parecerem nota
        var porChave = new Dictionary<string, PdfDoEmail>();
        var origemDaChave = new Dictionary<string, string>();
        PdfDoEmail? semChave = null;
        foreach (PdfDoEmail pdf in email.Pdfs)
        {
            if (pdf.Conteudo.Length == 0 || pdf.Conteudo.Length > TamanhoMaximoPdf)
            {
                continue;
            }
            string origem = "nome do arquivo";
            string? chave = ChaveAcesso.Procurar(pdf.Nome);
            if (chave == null)
            {
                chave = ProcurarNoPdf(pdf.Conteudo);
                origem = "PDF";
            }
            if (chave != null)
            {
                if (!porChave.ContainsKey(chave))
                {
                    porChave[chave] = pdf;
                    origemDaChave[chave] = origem;
                }
            }
            else if (semChave == null && PareceNota(pdf, email))
            {
                semChave = pdf;
            }
        }

        // Sem chave no PDF: tenta o assunto e o corpo do e-mail
        if (porChave.Count == 0 && semChave != null)
        {
            string? chave = ChaveAcesso.Procurar(email.Assunto);
            string origem = "assunto";
            if (chave == null)
            {
                chave = ChaveAcesso.Procurar(email.Corpo);
                origem = "corpo do e-mail";
            }
            if (chave != null)
            {
                porChave[chave] = semChave;
                origemDaChave[chave] = origem;
                semChave = null;
            }
        }

        int registradas = 0;
        foreach (KeyValuePair<string, PdfDoEmail> item in porChave)
        {
            string chave = item.Key;
            // O XML desta nota já chegou (em outro e-mail) ou ela já está pendente
            bool jaTemNota = await _db.NotasFiscais.AnyAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == chave, cancelamento);
            bool jaPendente = await _db.NotasSoPdf.AnyAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == chave, cancelamento);
            if (jaTemNota || jaPendente)
            {
                continue;
            }
            NotaSoPdf nota = Nova(clienteId, empresaDaCaixaId, email, item.Value);
            nota.ChaveAcesso = chave;
            nota.OrigemChave = origemDaChave[chave];
            nota.Situacao = SituacaoNotaSoPdf.BuscandoNaSefaz;
            _db.NotasSoPdf.Add(nota);
            registradas++;
        }

        if (semChave != null)
        {
            NotaSoPdf nota = Nova(clienteId, empresaDaCaixaId, email, semChave);
            nota.Situacao = SituacaoNotaSoPdf.SemChave;
            nota.Motivo = MotivoSemChave;
            _db.NotasSoPdf.Add(nota);
            registradas++;
        }

        if (registradas > 0)
        {
            await _db.SaveChangesAsync(cancelamento);
        }
        return registradas;
    }

    private static string? ProcurarNoPdf(byte[] pdf)
    {
        try
        {
            return TextoPdf.ProcurarChave(pdf);
        }
        catch (Exception)
        {
            // PDF fora do padrão: segue sem a chave
            return null;
        }
    }

    private static bool PareceNota(PdfDoEmail pdf, EmailSemXml email)
    {
        string nome = pdf.Nome.ToUpperInvariant();
        string assunto = email.Assunto.ToUpperInvariant();
        if (nome.Contains("BOLETO") || (assunto.Contains("BOLETO") && !assunto.Contains("NOTA")))
        {
            return false;
        }
        foreach (string sinal in SinaisDeNota)
        {
            if (nome.Contains(sinal) || assunto.Contains(sinal))
            {
                return true;
            }
        }
        if (assunto.Contains("NOTA FISCAL") || nome.StartsWith("NF") || assunto.StartsWith("NF"))
        {
            return true;
        }
        try
        {
            foreach (string texto in TextoPdf.Textos(pdf.Conteudo))
            {
                string maiusculo = texto.ToUpperInvariant();
                if (maiusculo.Contains("DANFE") || maiusculo.Contains("DOCUMENTO AUXILIAR DA NOTA FISCAL"))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }
        return false;
    }

    private static NotaSoPdf Nova(int clienteId, int? empresaDaCaixaId, EmailSemXml email, PdfDoEmail pdf)
    {
        var nota = new NotaSoPdf();
        nota.ClienteId = clienteId;
        nota.EmpresaCaixaId = empresaDaCaixaId;
        nota.MensagemId = Limitar(email.MensagemId, 300);
        nota.Remetente = Limitar(email.Remetente, 200);
        nota.NomeRemetente = Limitar(email.NomeRemetente, 200);
        nota.Assunto = Limitar(email.Assunto, 300);
        nota.OrigemEmail = Limitar(email.Origem, 300);
        nota.PdfNome = Limitar(pdf.Nome, 200);
        nota.Pdf = pdf.Conteudo;
        nota.RecebidaEm = DateTime.UtcNow;
        return nota;
    }

    private static string Limitar(string texto, int tamanho)
    {
        if (texto.Length <= tamanho)
        {
            return texto;
        }
        return texto.Substring(0, tamanho);
    }
}
