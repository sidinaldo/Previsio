using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Resumos;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Vencimentos;

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
    private readonly AlertaVencimentosOptions _opcoes;

    public AlertaVencimentosService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Usuario> usuarios,
        IRepositorio<Empresa> empresas,
        IRepositorio<ContaPagarSincronizada> contasPagar,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<AlertaVencimentosService> logger,
        IOptions<AlertaVencimentosOptions> opcoes)
    {
        _opcoes = opcoes.Value;
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
                    .Where(u => u.Ativo && u.Perfil != PerfilUsuario.Admin && u.ClienteId == cliente.Id)
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
        Dictionary<int, string> nomes = empresas.ToDictionary(e => e.Id, e => e.Nome);
        var html = new StringBuilder();
        var texto = new StringBuilder();
        html.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#0F172A;max-width:600px\">");
        html.Append("<p style=\"font-size:20px;font-weight:600;color:#0B1B36;margin:0 0 4px\">Previsio</p>");
        html.Append("<p style=\"color:#5E6E85;margin:0 0 20px\">Vencimentos · " + WebUtility.HtmlEncode(cliente.Nome) + " · " + hoje.ToString("dd/MM/yyyy", Brasil) + "</p>");
        html.Append("<p>Olá, {nome}.</p>");
        texto.Append("Previsio · Vencimentos · " + cliente.Nome + " · " + hoje.ToString("dd/MM/yyyy", Brasil) + "\n\nOlá, {nome}.\n\n");

        int maximo = _opcoes.MaximoContasPorBlocoValido();
        // Hoje e amanhã: as maiores primeiro; vencidas: as mais antigas primeiro
        Bloco(html, texto, "Vencem hoje", deHoje.OrderByDescending(c => c.Valor).ToList(), nomes,
            LinkDoDia(urlFront, hoje), hoje, maximo, false);
        Bloco(html, texto, "Vencem amanhã", deAmanha.OrderByDescending(c => c.Valor).ToList(), nomes,
            LinkDoDia(urlFront, amanha), hoje, maximo, false);
        if (vencidas.Count > 0)
        {
            string linha = Quantidade(vencidas.Count, "conta vencida", "contas vencidas") + " em aberto no Bling, somando "
                + ModeloEmailResumo.Dinheiro(vencidas.Sum(c => c.Valor)) + ". Pague ou dê baixa no Bling.";
            html.Append("<p style=\"padding:12px 16px;border:1px solid #FCA5A5;border-radius:8px;background:#FEF2F2;margin:16px 0 6px\"><strong style=\"color:#B91C1C\">Atenção:</strong> "
                + WebUtility.HtmlEncode(linha) + "</p>");
            texto.Append("ATENÇÃO: " + linha + "\n");
            Linhas(html, texto, vencidas.OrderBy(c => c.Vencimento).ThenByDescending(c => c.Valor).ToList(), nomes,
                LinkVencidas(urlFront), hoje, maximo, true);
        }
        html.Append("<p><a href=\"" + WebUtility.HtmlEncode(urlFront + "/pages/painel") + "\" style=\"color:#1D4ED8;font-weight:600\">Abrir o painel</a></p>");
        html.Append("<p style=\"color:#5E6E85;font-size:12px\">Contas em aberto no Bling, sincronizadas pelo Previsio. A hora deste e-mail é escolhida na tela Configurações.</p></div>");
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

    /// <summary>Contas a pagar em aberto que vencem no dia, já filtradas na tela.</summary>
    public static string LinkDoDia(string urlFront, DateTime dia)
    {
        string data = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return urlFront + "/pages/contas-pagar?situacao=Abertas&de=" + data + "&ate=" + data;
    }

    /// <summary>Contas a pagar vencidas e em aberto, já filtradas na tela.</summary>
    public static string LinkVencidas(string urlFront)
    {
        return urlFront + "/pages/contas-pagar?situacao=Vencidas";
    }

    // Título com o total e, com mais de uma loja, o total de cada uma; depois a lista das contas
    private static void Bloco(
        StringBuilder html, StringBuilder texto, string titulo, List<ContaPagarSincronizada> contas,
        Dictionary<int, string> empresas, string link, DateTime hoje, int maximo, bool atraso)
    {
        if (contas.Count == 0)
        {
            return;
        }
        string total = ModeloEmailResumo.Dinheiro(contas.Sum(c => c.Valor)) + " (" + Quantidade(contas.Count, "conta", "contas") + ")";
        html.Append("<p style=\"font-size:15px;font-weight:600;margin:16px 0 2px\">" + WebUtility.HtmlEncode(titulo) + ": "
            + WebUtility.HtmlEncode(total) + "</p>");
        texto.Append(titulo.ToUpper(Brasil) + ": " + total + "\n");

        var porLoja = new List<string>();
        foreach (var grupo in contas.GroupBy(c => c.EmpresaId).OrderBy(g => NomeDaLoja(empresas, g.Key)))
        {
            porLoja.Add(NomeDaLoja(empresas, grupo.Key) + " " + ModeloEmailResumo.Dinheiro(grupo.Sum(c => c.Valor)));
        }
        if (porLoja.Count > 1)
        {
            string lojas = "Por loja: " + string.Join(" · ", porLoja);
            html.Append("<p style=\"color:#5E6E85;font-size:12px;margin:0 0 6px\">" + WebUtility.HtmlEncode(lojas) + "</p>");
            texto.Append("  " + lojas + "\n");
        }
        Linhas(html, texto, contas, empresas, link, hoje, maximo, atraso);
    }

    // Tabela de até "maximo" contas (fornecedor, loja, valor e, nas vencidas, os dias de atraso) e o link filtrado
    private static void Linhas(
        StringBuilder html, StringBuilder texto, List<ContaPagarSincronizada> contas,
        Dictionary<int, string> empresas, string link, DateTime hoje, int maximo, bool atraso)
    {
        string celula = "padding:4px 8px 4px 0;border-bottom:1px solid #E3E8EF;vertical-align:top";
        html.Append("<table role=\"presentation\" style=\"width:100%;border-collapse:collapse;font-size:13px;margin:4px 0 6px\">");
        foreach (ContaPagarSincronizada conta in contas.Take(maximo))
        {
            string fornecedor = string.IsNullOrWhiteSpace(conta.FornecedorNome) ? "Fornecedor não identificado" : conta.FornecedorNome!;
            string loja = NomeDaLoja(empresas, conta.EmpresaId);
            string valor = ModeloEmailResumo.Dinheiro(conta.Valor);
            html.Append("<tr><td style=\"" + celula + "\">" + WebUtility.HtmlEncode(fornecedor) + "</td>"
                + "<td style=\"" + celula + ";color:#5E6E85\">" + WebUtility.HtmlEncode(loja) + "</td>");
            string linha = "  - " + fornecedor + " · " + loja + " · " + valor;
            if (atraso)
            {
                int dias = (hoje - conta.Vencimento.Date).Days;
                string textoAtraso = Quantidade(dias, "dia", "dias") + " de atraso";
                html.Append("<td style=\"" + celula + ";color:#B91C1C;white-space:nowrap\">" + WebUtility.HtmlEncode(textoAtraso) + "</td>");
                linha = linha + " · " + textoAtraso;
            }
            html.Append("<td style=\"" + celula + ";text-align:right;white-space:nowrap;font-weight:600\">" + WebUtility.HtmlEncode(valor) + "</td></tr>");
            texto.Append(linha + "\n");
        }
        html.Append("</table>");

        string verTudo = "Ver no Previsio";
        if (contas.Count > maximo)
        {
            int resto = contas.Count - maximo;
            string mais = "e mais " + Quantidade(resto, "conta", "contas");
            html.Append("<p style=\"color:#5E6E85;font-size:12px;margin:0 0 2px\">" + WebUtility.HtmlEncode(mais) + "</p>");
            texto.Append("  " + mais + "\n");
            verTudo = "Ver todas no Previsio";
        }
        html.Append("<p style=\"margin:0 0 12px\"><a href=\"" + WebUtility.HtmlEncode(link) + "\" style=\"color:#1D4ED8;font-weight:600\">"
            + verTudo + "</a></p>");
        texto.Append("  " + verTudo + ": " + link + "\n\n");
    }

    private static string NomeDaLoja(Dictionary<int, string> empresas, int empresaId)
    {
        if (empresas.ContainsKey(empresaId))
        {
            return empresas[empresaId];
        }
        return "";
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
