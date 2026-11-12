using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Sefaz;
using Previsio.Core.Comum;

namespace Previsio.Aplicacao.NotasSoPdf;

/// <summary>
/// Registra a nota que chegou só em PDF: procura a chave de acesso (nome do arquivo, assunto,
/// corpo do e-mail e texto do PDF) e deixa a nota na fila da SEFAZ ou pendente, sem chave.
/// PDF que não parece nota fiscal (boleto, pedido) é ignorado.
/// </summary>
public class RegistroNotaSoPdf
{
    public const int TamanhoMaximoPdf = 10 * 1024 * 1024;
    public const string MotivoSemChave = "Não achei a chave de acesso no PDF nem no e-mail. Informe a chave, anexe o XML ou peça o XML ao fornecedor.";
    public const string MotivoNaoPareceNota = "PDF não parece nota fiscal: sem chave de acesso e sem DANFE no texto, no nome do arquivo ou no assunto.";
    public const string MotivoPdfInvalido = "PDF vazio ou grande demais (mais de 10 MB).";

    private static readonly string[] SinaisDeNota = new string[]
    {
        "DANFE", "DOCUMENTO AUXILIAR DA NOTA FISCAL", "NOTA FISCAL ELETR", "NF-E", "NFE"
    };

    private readonly IRepositorio<NotaSoPdf> _notasSoPdf;
    private readonly IRepositorio<NotaFiscal> _notas;

    public RegistroNotaSoPdf(IRepositorio<NotaSoPdf> notasSoPdf, IRepositorio<NotaFiscal> notas)
    {
        _notasSoPdf = notasSoPdf;
        _notas = notas;
    }

    /// <returns>Quantas notas em PDF ficaram registradas.</returns>
    public async Task<int> RegistrarAsync(int clienteId, int? empresaDaCaixaId, EmailSemXml email, CancellationToken cancelamento)
    {
        ResultadoNotaSoPdf resultado = await AnalisarAsync(clienteId, empresaDaCaixaId, email, false, cancelamento);
        return resultado.Registradas;
    }

    /// <summary>
    /// Registra as notas em PDF do e-mail e diz por que nada entrou, quando for o caso.
    /// "forcar" trata o PDF sem chave como nota mesmo sem parecer (o admin pediu).
    /// </summary>
    public async Task<ResultadoNotaSoPdf> AnalisarAsync(
        int clienteId, int? empresaDaCaixaId, EmailSemXml email, bool forcar, CancellationToken cancelamento)
    {
        var resultado = new ResultadoNotaSoPdf();
        if (email.Pdfs.Count == 0)
        {
            resultado.MotivoIgnorado = MotivoPdfInvalido;
            return resultado;
        }

        // A mesma mensagem lida de novo (falha no meio da leitura) não duplica
        if (email.MensagemId != "")
        {
            bool jaRegistrado = await _notasSoPdf.Consultar().AnyAsync(
                n => n.ClienteId == clienteId && n.MensagemId == email.MensagemId, cancelamento);
            if (jaRegistrado)
            {
                resultado.JaConhecido = true;
                return resultado;
            }
        }

        // Uma pendência por chave achada; os PDFs sem chave só contam se parecerem nota
        var porChave = new Dictionary<string, PdfDoEmail>();
        var origemDaChave = new Dictionary<string, string>();
        PdfDoEmail? semChave = null;
        PdfDoEmail? primeiroUtilizavel = null;
        foreach (PdfDoEmail pdf in email.Pdfs)
        {
            if (pdf.Conteudo.Length == 0 || pdf.Conteudo.Length > TamanhoMaximoPdf)
            {
                continue;
            }
            if (primeiroUtilizavel == null)
            {
                primeiroUtilizavel = pdf;
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
            else if (semChave == null && (forcar || PareceNota(pdf, email)))
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
        int conhecidas = 0;
        foreach (KeyValuePair<string, PdfDoEmail> item in porChave)
        {
            string chave = item.Key;
            // O XML desta nota já chegou (em outro e-mail) ou ela já está pendente
            var notaExistente = await _notas.ConsultarSomenteLeitura()
                .Where(n => n.ClienteId == clienteId && n.ChaveAcesso == chave)
                .Select(n => new { n.Id, n.Numero, n.RecebidaEm })
                .FirstOrDefaultAsync(cancelamento);
            bool jaPendente = await _notasSoPdf.Consultar().AnyAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == chave, cancelamento);
            if (notaExistente != null || jaPendente)
            {
                conhecidas++;
                if (resultado.MotivoJaExiste == null)
                {
                    resultado.Pdf = item.Value;
                    if (notaExistente != null)
                    {
                        resultado.NotaFiscalExistenteId = notaExistente.Id;
                        resultado.NumeroNotaExistente = notaExistente.Numero;
                        DateTime recebida = TimeZoneInfo.ConvertTimeFromUtc(
                            DateTime.SpecifyKind(notaExistente.RecebidaEm, DateTimeKind.Utc), RelogioBrasil.Fuso);
                        resultado.MotivoJaExiste = "A nota deste PDF já está no Previsio (NF " + notaExistente.Numero
                            + ", recebida em " + recebida.ToString("dd/MM/yyyy HH:mm") + "). Nada foi criado.";
                    }
                    else
                    {
                        resultado.NumeroNotaExistente = ChaveAcesso.Numero(chave);
                        resultado.MotivoJaExiste = "Este PDF já está pendente em Notas pendentes (NF "
                            + ChaveAcesso.Numero(chave) + "), recebido em outro e-mail. Nada foi criado.";
                    }
                }
                continue;
            }
            NotaSoPdf nota = Nova(clienteId, empresaDaCaixaId, email, item.Value);
            nota.ChaveAcesso = chave;
            nota.OrigemChave = origemDaChave[chave];
            nota.Situacao = SituacaoNotaSoPdf.BuscandoNaSefaz;
            _notasSoPdf.Adicionar(nota);
            registradas++;
            if (resultado.Situacao == null)
            {
                resultado.Situacao = SituacaoNotaSoPdf.BuscandoNaSefaz;
            }
        }

        if (semChave != null)
        {
            NotaSoPdf nota = Nova(clienteId, empresaDaCaixaId, email, semChave);
            nota.Situacao = SituacaoNotaSoPdf.SemChave;
            nota.Motivo = MotivoSemChave;
            _notasSoPdf.Adicionar(nota);
            registradas++;
            if (resultado.Situacao == null)
            {
                resultado.Situacao = SituacaoNotaSoPdf.SemChave;
            }
        }

        if (registradas > 0)
        {
            await _notasSoPdf.SalvarAsync(cancelamento);
        }

        resultado.Registradas = registradas;
        if (registradas == 0)
        {
            if (conhecidas > 0)
            {
                resultado.JaConhecido = true;
            }
            else if (primeiroUtilizavel == null)
            {
                resultado.MotivoIgnorado = MotivoPdfInvalido;
            }
            else
            {
                resultado.MotivoIgnorado = MotivoNaoPareceNota;
                resultado.Pdf = primeiroUtilizavel;
            }
        }
        return resultado;
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
