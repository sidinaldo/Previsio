using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Contratos;
using Previsio.Api.Dtos;
using Previsio.Api.Resumos;
using Previsio.Api.Servicos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Vencimentos;

/// <summary>
/// Configuração dos avisos de vencimento e o e-mail diário com as contas que vencem hoje e amanhã
/// (e o lembrete das vencidas). Vai para os usuários da rede, uma vez por dia, na hora escolhida.
/// Sem nada vencendo hoje ou amanhã nem vencido, não manda.
/// </summary>
public class AlertaVencimentosService
{
    public const int MaximoDiasTolerancia = 60;

    private static readonly CultureInfo Brasil = new CultureInfo("pt-BR");

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Usuario> _usuarios;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<ContaPagarSincronizada> _contasPagar;
    private readonly IEnvioEmail _email;
    private readonly IConfiguration _configuracao;
    private readonly TimeProvider _relogio;
    private readonly ILogger<AlertaVencimentosService> _logger;

    public AlertaVencimentosService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Usuario> usuarios,
        IRepositorio<Empresa> empresas,
        IRepositorio<ContaPagarSincronizada> contasPagar,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<AlertaVencimentosService> logger)
    {
        _clientes = clientes;
        _usuarios = usuarios;
        _empresas = empresas;
        _contasPagar = contasPagar;
        _email = email;
        _configuracao = configuracao;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<Resultado<AlertasVencimentoDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<AlertasVencimentoDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        return Resultado<AlertasVencimentoDto>.Ok(ParaDto(cliente));
    }

    public async Task<Resultado<AlertasVencimentoDto>> AlterarAsync(int clienteId, AlertasVencimentoDto dados, CancellationToken cancelamento)
    {
        if (dados.AvisoVencidasDias < 1 || dados.AvisoVencidasDias > MaximoDiasTolerancia)
        {
            return Resultado<AlertasVencimentoDto>.Invalido("Os dias de atraso vão de 1 a " + MaximoDiasTolerancia + ".");
        }
        if (dados.AlertaDiarioHora < 0 || dados.AlertaDiarioHora > 23)
        {
            return Resultado<AlertasVencimentoDto>.Invalido("Escolha uma hora entre 0 e 23.");
        }
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<AlertasVencimentoDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        cliente.AvisoVencidasDias = dados.AvisoVencidasDias;
        cliente.AlertaVencimentosAtivo = dados.AlertaDiarioAtivo;
        cliente.AlertaVencimentosHora = dados.AlertaDiarioHora;
        await _clientes.SalvarAsync(cancelamento);
        return Resultado<AlertasVencimentoDto>.Ok(ParaDto(cliente));
    }

    /// <summary>
    /// De hora em hora: manda o alerta das redes cuja hora chegou e que ainda não receberam hoje.
    /// </summary>
    public async Task EnviarDevidosAsync(CancellationToken cancelamento)
    {
        DateTime agoraUtc = _relogio.GetUtcNow().UtcDateTime;
        DateTime agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, RelogioBrasil.Fuso);
        DateTime hoje = agoraLocal.Date;

        List<int> redes = await _clientes.Consultar().Where(PeriodoTeste.Processa(agoraUtc)).Select(c => c.Id).ToListAsync(cancelamento);
        List<Cliente> clientes = await _clientes.Consultar()
            .Where(c => redes.Contains(c.Id) && c.AlertaVencimentosAtivo && c.AlertaVencimentosHora <= agoraLocal.Hour)
            .ToListAsync(cancelamento);

        foreach (Cliente cliente in clientes)
        {
            if (cliente.AlertaVencimentosEnviadoEm.HasValue)
            {
                DateTime enviado = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(cliente.AlertaVencimentosEnviadoEm.Value, DateTimeKind.Utc), RelogioBrasil.Fuso);
                if (enviado.Date == hoje)
                {
                    continue;
                }
            }

            MensagemEmail? modelo = await MontarAsync(cliente, hoje, cancelamento);
            if (modelo != null && _email.Configurado)
            {
                List<Usuario> usuarios = await _usuarios.ConsultarSomenteLeitura()
                    .Where(u => u.Ativo && u.Perfil == PerfilUsuario.Cliente && u.ClienteId == cliente.Id)
                    .ToListAsync(cancelamento);
                foreach (Usuario usuario in usuarios)
                {
                    var mensagem = new MensagemEmail();
                    mensagem.Para = usuario.Email;
                    mensagem.NomePara = usuario.Nome;
                    mensagem.Assunto = modelo.Assunto;
                    mensagem.Html = modelo.Html.Replace("{nome}", WebUtility.HtmlEncode(usuario.Nome));
                    mensagem.Texto = modelo.Texto.Replace("{nome}", usuario.Nome);
                    try
                    {
                        await _email.EnviarAsync(mensagem, cancelamento);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception erro)
                    {
                        _logger.LogError(erro, "Falha ao enviar o alerta de vencimentos ao usuário {UsuarioId}", usuario.Id);
                    }
                }
            }
            cliente.AlertaVencimentosEnviadoEm = agoraUtc;
        }
        await _clientes.SalvarAsync(cancelamento);
    }

    /// <summary>
    /// O e-mail do dia (com "{nome}" no lugar do nome do usuário); nulo quando não há nada a avisar.
    /// </summary>
    public async Task<MensagemEmail?> MontarAsync(Cliente cliente, DateTime hoje, CancellationToken cancelamento)
    {
        var empresas = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == cliente.Id && e.Ativo)
            .Select(e => new { e.Id, e.Nome })
            .ToListAsync(cancelamento);
        List<int> ids = empresas.Select(e => e.Id).ToList();
        DateTime amanha = hoje.AddDays(1);

        List<ContaPagarSincronizada> proximas = await ContasEmAberto.Das(_contasPagar.ConsultarSomenteLeitura(), ids)
            .Where(c => c.Vencimento >= hoje && c.Vencimento <= amanha)
            .ToListAsync(cancelamento);
        List<ContaPagarSincronizada> vencidas = await ContasEmAberto.Vencidas(_contasPagar.ConsultarSomenteLeitura(), ids, hoje, cliente.AvisoVencidasDias)
            .ToListAsync(cancelamento);

        List<ContaPagarSincronizada> deHoje = proximas.Where(c => c.Vencimento.Date == hoje).ToList();
        List<ContaPagarSincronizada> deAmanha = proximas.Where(c => c.Vencimento.Date == amanha).ToList();
        if (deHoje.Count == 0 && deAmanha.Count == 0 && vencidas.Count == 0)
        {
            return null;
        }

        string urlFront = (_configuracao["Front:Url"] ?? "http://localhost:4200/#").TrimEnd('/');
        var html = new StringBuilder();
        var texto = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#0F172A;max-width:600px\">");
        html.Append("<p style=\"font-size:20px;font-weight:600;color:#0B1B36;margin:0 0 4px\">Previsio</p>");
        html.Append("<p style=\"color:#5E6E85;margin:0 0 20px\">Vencimentos · " + WebUtility.HtmlEncode(cliente.Nome) + " · " + hoje.ToString("dd/MM/yyyy", Brasil) + "</p>");
        html.Append("<p>Olá, {nome}.</p>");
        texto.Append("Previsio · Vencimentos · " + cliente.Nome + " · " + hoje.ToString("dd/MM/yyyy", Brasil) + "\n\nOlá, {nome}.\n\n");

        Bloco(html, texto, "Vencem hoje", deHoje, empresas.ToDictionary(e => e.Id, e => e.Nome));
        Bloco(html, texto, "Vencem amanhã", deAmanha, empresas.ToDictionary(e => e.Id, e => e.Nome));
        if (vencidas.Count > 0)
        {
            string linha = Quantidade(vencidas.Count, "conta vencida", "contas vencidas") + " em aberto no Bling, somando "
                + ModeloEmailResumo.Dinheiro(vencidas.Sum(c => c.Valor)) + ". Pague ou dê baixa no Bling.";
            html.Append("<p style=\"padding:12px 16px;border:1px solid #FCA5A5;border-radius:8px;background:#FEF2F2\"><strong style=\"color:#B91C1C\">Atenção:</strong> "
                + WebUtility.HtmlEncode(linha) + "</p>");
            texto.Append("ATENÇÃO: " + linha + "\n\n");
        }
        html.Append("<p><a href=\"" + WebUtility.HtmlEncode(urlFront + "/pages/painel") + "\" style=\"color:#1D4ED8;font-weight:600\">Abrir o painel</a></p>");
        html.Append("<p style=\"color:#5E6E85;font-size:12px\">Contas em aberto no Bling, sincronizadas pelo Previsio. A hora deste e-mail é escolhida na tela Empresas.</p></div>");
        texto.Append("Abrir o painel: " + urlFront + "/pages/painel\n");

        decimal totalHoje = deHoje.Sum(c => c.Valor);
        string assunto;
        if (deHoje.Count > 0)
        {
            assunto = "Previsio: hoje vencem " + ModeloEmailResumo.Dinheiro(totalHoje) + " em " + Quantidade(deHoje.Count, "conta", "contas");
        }
        else if (deAmanha.Count > 0)
        {
            assunto = "Previsio: amanhã vencem " + ModeloEmailResumo.Dinheiro(deAmanha.Sum(c => c.Valor)) + " em " + Quantidade(deAmanha.Count, "conta", "contas");
        }
        else
        {
            assunto = "Previsio: " + Quantidade(vencidas.Count, "conta vencida", "contas vencidas") + " em aberto";
        }

        var mensagem = new MensagemEmail();
        mensagem.Assunto = assunto + " · " + cliente.Nome;
        mensagem.Html = html.ToString();
        mensagem.Texto = texto.ToString();
        return mensagem;
    }

    private static void Bloco(StringBuilder html, StringBuilder texto, string titulo, List<ContaPagarSincronizada> contas, Dictionary<int, string> empresas)
    {
        if (contas.Count == 0)
        {
            return;
        }
        decimal total = contas.Sum(c => c.Valor);
        html.Append("<p style=\"font-size:15px;font-weight:600;margin:16px 0 6px\">" + WebUtility.HtmlEncode(titulo) + ": "
            + WebUtility.HtmlEncode(ModeloEmailResumo.Dinheiro(total)) + "</p><ul style=\"padding-left:20px;margin:0 0 12px\">");
        texto.Append(titulo.ToUpper(Brasil) + ": " + ModeloEmailResumo.Dinheiro(total) + "\n");
        foreach (var grupo in contas.GroupBy(c => c.EmpresaId).OrderBy(g => empresas.ContainsKey(g.Key) ? empresas[g.Key] : ""))
        {
            string nome = empresas.ContainsKey(grupo.Key) ? empresas[grupo.Key] : "";
            string linha = nome + " · " + Quantidade(grupo.Count(), "conta", "contas") + " · " + ModeloEmailResumo.Dinheiro(grupo.Sum(c => c.Valor));
            html.Append("<li>" + WebUtility.HtmlEncode(linha) + "</li>");
            texto.Append("  " + linha + "\n");
        }
        html.Append("</ul>");
        texto.Append("\n");
    }

    private static string Quantidade(int quantidade, string singular, string plural)
    {
        if (quantidade == 1)
        {
            return "1 " + singular;
        }
        return quantidade + " " + plural;
    }

    private static AlertasVencimentoDto ParaDto(Cliente cliente)
    {
        var dto = new AlertasVencimentoDto();
        dto.AvisoVencidasDias = cliente.AvisoVencidasDias;
        dto.AlertaDiarioAtivo = cliente.AlertaVencimentosAtivo;
        dto.AlertaDiarioHora = cliente.AlertaVencimentosHora;
        return dto;
    }
}
