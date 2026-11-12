using System.Net;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Sefaz;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;
using Previsio.Core.Sefaz;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.NotasSoPdf;

/// <summary>
/// O que o financeiro faz com a nota que chegou só em PDF: anexar o XML, informar a chave,
/// pedir o XML ao fornecedor ou marcar que já lançou no Bling.
/// </summary>
public class NotasSoPdfService
{
    public const string MensagemNaoEncontrada = "Nota em PDF não encontrada.";
    public const string MensagemJaResolvida = "Essa nota já foi resolvida.";
    public const string MensagemXmlInvalido = "O arquivo não é o XML de uma NF-e.";
    public const string MensagemChaveInvalida = "Chave de acesso inválida: confira os 44 dígitos.";
    public const string MensagemSemRemetente = "O e-mail não tem o endereço do remetente para responder.";
    public const string MensagemSmtpNaoConfigurado = "O envio de e-mail não está configurado no servidor (seção Smtp).";
    public const string MensagemFalhaEnvio = "Não consegui enviar o e-mail ao fornecedor. Tente de novo.";

    private static readonly SituacaoNotaSoPdf[] Abertas = new SituacaoNotaSoPdf[]
    {
        SituacaoNotaSoPdf.BuscandoNaSefaz,
        SituacaoNotaSoPdf.SemChave,
        SituacaoNotaSoPdf.SemCertificado,
        SituacaoNotaSoPdf.NaoResolvida
    };

    private readonly PrevisioDbContext _db;
    private readonly RegistroNotaService _registro;
    private readonly LeitorNFe _leitor;
    private readonly IEnvioEmail _email;
    private readonly TimeProvider _relogio;
    private readonly ILogger<NotasSoPdfService> _logger;

    public NotasSoPdfService(
        PrevisioDbContext db,
        RegistroNotaService registro,
        LeitorNFe leitor,
        IEnvioEmail email,
        TimeProvider relogio,
        ILogger<NotasSoPdfService> logger)
    {
        _db = db;
        _registro = registro;
        _leitor = leitor;
        _email = email;
        _relogio = relogio;
        _logger = logger;
    }

    public static bool EstaAberta(SituacaoNotaSoPdf situacao)
    {
        return Abertas.Contains(situacao);
    }

    /// <summary>
    /// Notas em PDF ainda abertas da rede. Com empresa: as que chegaram na caixa dela
    /// ou que a SEFAZ já confirmou como dela.
    /// </summary>
    public async Task<List<NotaSoPdfDto>> ListarAsync(int clienteId, int? empresaId, CancellationToken cancelamento)
    {
        IQueryable<NotaSoPdf> consulta = _db.NotasSoPdf
            .AsNoTracking()
            .Include(n => n.EmpresaCaixa)
            .Where(n => n.ClienteId == clienteId && Abertas.Contains(n.Situacao));
        if (empresaId.HasValue)
        {
            int id = empresaId.Value;
            consulta = consulta.Where(n => n.EmpresaCaixaId == id || n.EmpresaDestinoId == id);
        }
        List<NotaSoPdf> notas = await consulta.OrderBy(n => n.RecebidaEm).ToListAsync(cancelamento);

        var lista = new List<NotaSoPdfDto>();
        foreach (NotaSoPdf nota in notas)
        {
            lista.Add(ParaDto(nota));
        }
        return lista;
    }

    /// <summary>
    /// O PDF original, para conferir. Nulo se não existir ou se o usuário não puder ver.
    /// </summary>
    public async Task<NotaSoPdf?> ObterAsync(int id, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        NotaSoPdf? nota = await _db.NotasSoPdf.FirstOrDefaultAsync(n => n.Id == id, cancelamento);
        if (nota == null || !usuario.PodeAcessarCliente(nota.ClienteId))
        {
            return null;
        }
        return nota;
    }

    public async Task<Resultado> AnexarXmlAsync(int id, string xml, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Resultado<NotaSoPdf> busca = await AbertaAsync(id, usuario, cancelamento);
        if (!busca.Sucesso)
        {
            return Falha(busca);
        }
        NotaSoPdf item = busca.Valor!;

        NotaFiscalXml? nota = _leitor.Ler(xml ?? "");
        if (nota == null)
        {
            return Resultado.Invalido(MensagemXmlInvalido);
        }
        if (item.ChaveAcesso != null && item.ChaveAcesso != nota.ChaveAcesso)
        {
            return Resultado.Invalido("Esse XML é de outra nota (NF " + nota.Numero + "). O PDF é da NF "
                + ChaveAcesso.Numero(item.ChaveAcesso) + ".");
        }

        await _registro.RegistrarAsync(item.ClienteId, nota, item.OrigemEmail, item.EmpresaCaixaId, cancelamento);
        int? notaId = await _db.NotasFiscais
            .Where(n => n.ClienteId == item.ClienteId && n.ChaveAcesso == nota.ChaveAcesso)
            .Select(n => (int?)n.Id)
            .FirstOrDefaultAsync(cancelamento);

        item.ChaveAcesso = nota.ChaveAcesso;
        item.Situacao = SituacaoNotaSoPdf.Resolvida;
        item.NotaFiscalId = notaId;
        item.ResolvidaEm = Agora();
        item.ResolvidaPor = Limitar("XML anexado por " + (usuario.Identity?.Name ?? "usuário"), 200);
        item.ProximaTentativaEm = null;
        item.Motivo = null;
        await _db.SaveChangesAsync(cancelamento);
        return Resultado.Ok();
    }

    public async Task<Resultado> InformarChaveAsync(int id, string chave, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Resultado<NotaSoPdf> busca = await AbertaAsync(id, usuario, cancelamento);
        if (!busca.Sucesso)
        {
            return Falha(busca);
        }
        NotaSoPdf item = busca.Valor!;

        string digitos = ChaveAcesso.SomenteDigitos(chave ?? "");
        if (!ChaveAcesso.Valida(digitos))
        {
            return Resultado.Invalido(MensagemChaveInvalida);
        }

        bool outraComAMesmaChave = await _db.NotasSoPdf.AnyAsync(
            n => n.ClienteId == item.ClienteId && n.Id != item.Id && n.ChaveAcesso == digitos && Abertas.Contains(n.Situacao), cancelamento);
        if (outraComAMesmaChave)
        {
            return Resultado.Conflito("Já existe outra nota em PDF pendente com essa chave.");
        }

        item.ChaveAcesso = digitos;
        item.OrigemChave = "informada";
        item.Situacao = SituacaoNotaSoPdf.BuscandoNaSefaz;
        item.TentativasSefaz = 0;
        item.CienciaEnviadaEm = null;
        item.ProximaTentativaEm = null;
        item.Motivo = null;

        // A nota pode já existir (o XML chegou por outro e-mail)
        NotaFiscal? existente = await _db.NotasFiscais.FirstOrDefaultAsync(
            n => n.ClienteId == item.ClienteId && n.ChaveAcesso == digitos, cancelamento);
        if (existente != null)
        {
            item.Situacao = SituacaoNotaSoPdf.Resolvida;
            item.NotaFiscalId = existente.Id;
            item.ResolvidaEm = Agora();
            item.ResolvidaPor = "XML recebido em outro e-mail";
        }
        await _db.SaveChangesAsync(cancelamento);
        return Resultado.Ok();
    }

    /// <summary>
    /// Responde ao fornecedor pedindo o XML; a resposta vai para a caixa que recebe as notas.
    /// </summary>
    public async Task<Resultado> PedirXmlAsync(int id, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Resultado<NotaSoPdf> busca = await AbertaAsync(id, usuario, cancelamento);
        if (!busca.Sucesso)
        {
            return Falha(busca);
        }
        NotaSoPdf item = busca.Valor!;
        if (item.Remetente.Trim() == "")
        {
            return Resultado.Invalido(MensagemSemRemetente);
        }
        if (!_email.Configurado)
        {
            return Resultado.Invalido(MensagemSmtpNaoConfigurado);
        }

        Cliente? rede = await _db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == item.ClienteId, cancelamento);
        string caixa = "";
        if (item.EmpresaCaixaId.HasValue)
        {
            Empresa? empresa = await _db.Empresas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == item.EmpresaCaixaId.Value, cancelamento);
            if (empresa != null)
            {
                caixa = empresa.ImapUsuario;
            }
        }
        if (caixa == "" && rede != null)
        {
            caixa = rede.ImapUsuario;
        }

        try
        {
            await _email.EnviarAsync(PedidoDeXml(item, rede, caixa), cancelamento);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha ao pedir o XML da nota em PDF {Id} ao fornecedor", item.Id);
            return Resultado.Invalido(MensagemFalhaEnvio);
        }

        item.XmlPedidoEm = Agora();
        await _db.SaveChangesAsync(cancelamento);
        return Resultado.Ok();
    }

    public async Task<Resultado> MarcarLancadaAsync(int id, string? observacao, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Resultado<NotaSoPdf> busca = await AbertaAsync(id, usuario, cancelamento);
        if (!busca.Sucesso)
        {
            return Falha(busca);
        }
        NotaSoPdf item = busca.Valor!;
        string texto = (observacao ?? "").Trim();
        if (texto.Length > 500)
        {
            return Resultado.Invalido("A observação pode ter até 500 caracteres.");
        }
        item.Situacao = SituacaoNotaSoPdf.LancadaManualmente;
        item.ResolvidaEm = Agora();
        item.ResolvidaPor = Limitar(usuario.Identity?.Name ?? "usuário", 200);
        item.Observacao = texto == "" ? null : texto;
        item.ProximaTentativaEm = null;
        await _db.SaveChangesAsync(cancelamento);
        return Resultado.Ok();
    }

    public static NotaSoPdfDto ParaDto(NotaSoPdf nota)
    {
        var dto = new NotaSoPdfDto();
        dto.Id = nota.Id;
        dto.RecebidaEm = nota.RecebidaEm;
        dto.Remetente = nota.Remetente;
        dto.NomeRemetente = nota.NomeRemetente;
        dto.Assunto = nota.Assunto;
        dto.PdfNome = nota.PdfNome;
        if (nota.EmpresaCaixa != null)
        {
            dto.EmpresaCaixaNome = nota.EmpresaCaixa.Nome;
        }
        dto.ChaveAcesso = nota.ChaveAcesso;
        dto.OrigemChave = nota.OrigemChave;
        if (nota.ChaveAcesso != null && nota.ChaveAcesso.Length == 44)
        {
            dto.NumeroNota = ChaveAcesso.Numero(nota.ChaveAcesso);
            dto.CnpjFornecedor = ChaveAcesso.CnpjEmitente(nota.ChaveAcesso);
        }
        dto.Situacao = nota.Situacao.ToString();
        dto.Motivo = nota.Motivo;
        dto.TentativasSefaz = nota.TentativasSefaz;
        dto.ProximaTentativaEm = nota.ProximaTentativaEm;
        dto.CienciaEnviadaEm = nota.CienciaEnviadaEm;
        dto.XmlPedidoEm = nota.XmlPedidoEm;
        return dto;
    }

    private async Task<Resultado<NotaSoPdf>> AbertaAsync(int id, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        NotaSoPdf? item = await ObterAsync(id, usuario, cancelamento);
        if (item == null)
        {
            return Resultado<NotaSoPdf>.NaoEncontrado(MensagemNaoEncontrada);
        }
        if (!EstaAberta(item.Situacao))
        {
            return Resultado<NotaSoPdf>.Conflito(MensagemJaResolvida);
        }
        return Resultado<NotaSoPdf>.Ok(item);
    }

    private static Resultado Falha(Resultado<NotaSoPdf> busca)
    {
        if (busca.Tipo == TipoResultado.NaoEncontrado)
        {
            return Resultado.NaoEncontrado(busca.Mensagem);
        }
        return Resultado.Conflito(busca.Mensagem ?? MensagemJaResolvida);
    }

    private static MensagemEmail PedidoDeXml(NotaSoPdf item, Cliente? rede, string caixa)
    {
        string nota = "a nota fiscal";
        if (item.ChaveAcesso != null)
        {
            nota = "a NF-e " + ChaveAcesso.Numero(item.ChaveAcesso);
        }
        string empresa = rede == null ? "" : " (" + rede.Nome + ")";
        string recebida = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(item.RecebidaEm, DateTimeKind.Utc), RelogioBrasil.Fuso)
            .ToString("dd/MM/yyyy");
        string destino = caixa != "" ? caixa : "este endereço";

        string texto = "Olá,\n\n"
            + "Recebemos " + nota + " em PDF no dia " + recebida + ", mas sem o arquivo XML.\n"
            + "Por favor, envie o XML da nota para " + destino + ". Precisamos dele para lançar a conta a pagar.\n";
        if (item.ChaveAcesso != null)
        {
            texto = texto + "\nChave de acesso: " + item.ChaveAcesso + "\n";
        }
        texto = texto + "\nE-mail original: " + item.Assunto + "\n\nObrigado" + empresa + ".";

        var html = new System.Text.StringBuilder();
        foreach (string linha in texto.Split('\n'))
        {
            html.Append(WebUtility.HtmlEncode(linha)).Append("<br>");
        }

        var mensagem = new MensagemEmail();
        mensagem.Para = item.Remetente;
        mensagem.NomePara = item.NomeRemetente;
        mensagem.Assunto = "Pedido do XML: " + (item.Assunto == "" ? nota : item.Assunto);
        mensagem.Texto = texto;
        mensagem.Html = "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px\">" + html + "</div>";
        mensagem.ResponderPara = caixa;
        return mensagem;
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
