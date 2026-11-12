using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.NotasSoPdf;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.EmailsIgnorados;

/// <summary>
/// E-mails com anexo que a leitura examinou e não viraram nota nem pendência: registro, lista para o admin,
/// PDF para conferir e "Tratar como nota em PDF".
/// </summary>
public class EmailsIgnoradosService
{
    public const int MaximoNaLista = 200;
    public const string MensagemNaoEncontrado = "E-mail ignorado não encontrado (pode ter passado do prazo e sido apagado).";

    private readonly IRepositorio<EmailIgnorado> _emailsIgnorados;
    private readonly RegistroNotaSoPdf _notasSoPdf;
    private readonly TimeProvider _relogio;
    private readonly LeituraEmailOptions _opcoes;

    public EmailsIgnoradosService(
        IRepositorio<EmailIgnorado> emailsIgnorados,
        RegistroNotaSoPdf notasSoPdf,
        TimeProvider relogio,
        IOptions<LeituraEmailOptions> opcoes)
    {
        _emailsIgnorados = emailsIgnorados;
        _notasSoPdf = notasSoPdf;
        _relogio = relogio;
        _opcoes = opcoes.Value;
    }

    /// <summary>
    /// Guarda o e-mail ignorado. O mesmo e-mail lido de novo (caixa relida) não duplica. Quem chama salva.
    /// </summary>
    public async Task RegistrarAsync(EmailParaRegistrar email, CancellationToken cancelamento)
    {
        bool jaExiste;
        if (email.MensagemId != "")
        {
            jaExiste = await _emailsIgnorados.Consultar().AnyAsync(
                e => e.ClienteId == email.ClienteId && e.MensagemId == email.MensagemId, cancelamento);
        }
        else
        {
            jaExiste = await _emailsIgnorados.Consultar().AnyAsync(
                e => e.ClienteId == email.ClienteId && e.EmpresaCaixaId == email.EmpresaCaixaId && e.Uid == email.Uid, cancelamento);
        }
        if (jaExiste)
        {
            return;
        }

        var registro = new EmailIgnorado();
        registro.ClienteId = email.ClienteId;
        registro.EmpresaCaixaId = email.EmpresaCaixaId;
        registro.Uid = email.Uid;
        registro.MensagemId = Limitar(email.MensagemId, 300);
        registro.Remetente = Limitar(email.Remetente, 200);
        registro.NomeRemetente = Limitar(email.NomeRemetente, 200);
        registro.Assunto = Limitar(email.Assunto, 300);
        registro.Corpo = Limitar(email.Corpo, 4000);
        registro.EnviadoEm = email.EnviadoEm;
        registro.LidoEm = Agora();
        registro.Anexos = Limitar(string.Join(", ", email.Anexos), 500);
        registro.Motivo = Limitar(email.Motivo, 300);
        if (email.Pdf != null)
        {
            registro.PdfNome = Limitar(email.PdfNome ?? "", 200);
            registro.Pdf = email.Pdf;
        }
        registro.NotaFiscalId = email.NotaFiscalId;
        if (email.NumeroNota != null)
        {
            registro.NumeroNota = Limitar(email.NumeroNota, 20);
        }
        _emailsIgnorados.Adicionar(registro);
    }

    /// <summary>
    /// Apaga os que passaram do prazo (seção LeituraEmail, padrão 30 dias). Devolve quantos apagou.
    /// </summary>
    public async Task<int> LimparAntigosAsync(CancellationToken cancelamento)
    {
        DateTime limite = Agora().AddDays(-_opcoes.DiasGuardarIgnoradosValidos());
        List<EmailIgnorado> antigos = await _emailsIgnorados.Consultar().Where(e => e.LidoEm < limite).ToListAsync(cancelamento);
        if (antigos.Count == 0)
        {
            return 0;
        }
        foreach (EmailIgnorado antigo in antigos)
        {
            _emailsIgnorados.Remover(antigo);
        }
        await _emailsIgnorados.SalvarAsync(cancelamento);
        return antigos.Count;
    }

    /// <summary>
    /// Os lidos nos últimos N dias (1 a 30), do mais recente para o mais antigo, de uma rede ou de todas.
    /// </summary>
    public async Task<List<EmailIgnoradoDto>> ListarAsync(int? clienteId, int dias, CancellationToken cancelamento)
    {
        if (dias < 1 || dias > 30)
        {
            dias = 7;
        }
        DateTime desde = Agora().AddDays(-dias);
        IQueryable<EmailIgnorado> consulta = _emailsIgnorados.ConsultarSomenteLeitura()
            .Include(e => e.Cliente)
            .Include(e => e.EmpresaCaixa)
            .Where(e => e.LidoEm >= desde);
        if (clienteId.HasValue)
        {
            int id = clienteId.Value;
            consulta = consulta.Where(e => e.ClienteId == id);
        }
        List<EmailIgnorado> lista = await consulta
            .OrderByDescending(e => e.LidoEm)
            .ThenByDescending(e => e.Id)
            .Take(MaximoNaLista)
            .ToListAsync(cancelamento);

        var resposta = new List<EmailIgnoradoDto>();
        foreach (EmailIgnorado email in lista)
        {
            resposta.Add(ParaDto(email));
        }
        return resposta;
    }

    public async Task<EmailIgnorado?> ObterAsync(int id, CancellationToken cancelamento)
    {
        return await _emailsIgnorados.ConsultarSomenteLeitura().FirstOrDefaultAsync(e => e.Id == id, cancelamento);
    }

    /// <summary>
    /// O admin decidiu que o PDF é nota: vira pendência em Notas pendentes (busca na SEFAZ ou "Sem chave").
    /// </summary>
    public async Task<Resultado<TratamentoEmailIgnoradoDto>> TratarComoNotaAsync(int id, string usuario, CancellationToken cancelamento)
    {
        EmailIgnorado? email = await _emailsIgnorados.Consultar()
            .Include(e => e.Cliente)
            .Include(e => e.EmpresaCaixa)
            .FirstOrDefaultAsync(e => e.Id == id, cancelamento);
        if (email == null)
        {
            return Resultado<TratamentoEmailIgnoradoDto>.NaoEncontrado(MensagemNaoEncontrado);
        }
        if (email.TratadoEm != null)
        {
            return Resultado<TratamentoEmailIgnoradoDto>.Conflito("Este e-mail já foi tratado como nota.");
        }
        if (email.Pdf == null || email.Pdf.Length == 0)
        {
            return Resultado<TratamentoEmailIgnoradoDto>.Invalido("Este e-mail não tem PDF para tratar como nota.");
        }
        if (email.NumeroNota != null)
        {
            return Resultado<TratamentoEmailIgnoradoDto>.Invalido("A nota deste PDF já está no Previsio (NF " + email.NumeroNota + ").");
        }

        var semXml = new EmailSemXml();
        semXml.MensagemId = email.MensagemId;
        semXml.Remetente = email.Remetente;
        semXml.NomeRemetente = email.NomeRemetente;
        semXml.Assunto = email.Assunto;
        semXml.Corpo = email.Corpo;
        semXml.Origem = email.Remetente + " | " + email.Assunto;
        var pdf = new PdfDoEmail();
        pdf.Nome = email.PdfNome ?? "";
        pdf.Conteudo = email.Pdf;
        semXml.Pdfs.Add(pdf);

        ResultadoNotaSoPdf analise = await _notasSoPdf.AnalisarAsync(email.ClienteId, email.EmpresaCaixaId, semXml, true, cancelamento);

        string mensagem;
        if (analise.Registradas > 0 && analise.Situacao == SituacaoNotaSoPdf.BuscandoNaSefaz)
        {
            mensagem = "Virou pendência em Notas pendentes: a chave foi encontrada e o XML será buscado na SEFAZ.";
        }
        else if (analise.Registradas > 0)
        {
            mensagem = "Virou pendência em Notas pendentes, sem chave de acesso: informe a chave ou anexe o XML lá.";
        }
        else if (analise.JaConhecido)
        {
            mensagem = "A nota deste PDF já está no Previsio. Nada foi criado.";
        }
        else
        {
            return Resultado<TratamentoEmailIgnoradoDto>.Invalido("Não foi possível usar o PDF deste e-mail.");
        }

        email.TratadoEm = Agora();
        email.TratadoPor = Limitar(usuario, 200);
        email.ResultadoTratamento = Limitar(mensagem, 300);
        await _emailsIgnorados.SalvarAsync(cancelamento);

        var resposta = new TratamentoEmailIgnoradoDto();
        resposta.Mensagem = mensagem;
        resposta.Email = ParaDto(email);
        return Resultado<TratamentoEmailIgnoradoDto>.Ok(resposta);
    }

    private static EmailIgnoradoDto ParaDto(EmailIgnorado email)
    {
        var dto = new EmailIgnoradoDto();
        dto.Id = email.Id;
        dto.ClienteId = email.ClienteId;
        dto.ClienteNome = email.Cliente == null ? "" : email.Cliente.Nome;
        dto.Caixa = email.EmpresaCaixa == null ? "Caixa da rede" : email.EmpresaCaixa.Nome;
        if (email.NomeRemetente != "")
        {
            dto.Remetente = email.NomeRemetente + " <" + email.Remetente + ">";
        }
        else
        {
            dto.Remetente = email.Remetente;
        }
        dto.Assunto = email.Assunto;
        dto.EnviadoEm = email.EnviadoEm;
        dto.LidoEm = email.LidoEm;
        dto.Anexos = email.Anexos;
        dto.Motivo = email.Motivo;
        dto.TemPdf = email.Pdf != null && email.Pdf.Length > 0;
        dto.NotaFiscalId = email.NotaFiscalId;
        dto.NumeroNota = email.NumeroNota;
        dto.PodeTratar = dto.TemPdf && email.NumeroNota == null && email.TratadoEm == null;
        dto.TratadoEm = email.TratadoEm;
        dto.TratadoPor = email.TratadoPor;
        dto.ResultadoTratamento = email.ResultadoTratamento;
        return dto;
    }

    private DateTime Agora()
    {
        return _relogio.GetUtcNow().UtcDateTime;
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
