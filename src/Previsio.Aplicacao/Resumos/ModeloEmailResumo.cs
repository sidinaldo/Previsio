using Previsio.Aplicacao.Vencimentos;
using System.Globalization;
using System.Net;
using System.Text;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Aplicacao.Resumos;

/// <summary>
/// Monta o e-mail do resumo semanal (HTML simples, em tabelas, para abrir bem em qualquer leitor).
/// </summary>
public static class ModeloEmailResumo
{
    public const int MaximoProblemas = 10;

    private static readonly CultureInfo Brasil = new CultureInfo("pt-BR");
    private static readonly string[] DiasCurtos = new string[] { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };

    private const string Fonte = "font-family:Arial,Helvetica,sans-serif;";
    private const string CorTexto = "#0F172A";
    private const string CorSecundaria = "#475569";
    private const string CorAuxiliar = "#5E6E85";
    private const string CorBorda = "#E2E8F0";

    public static MensagemEmail Montar(ResumoSemanal resumo, Usuario usuario, string urlFront)
    {
        var html = new StringBuilder();
        var texto = new StringBuilder();

        html.Append("<div style=\"" + Fonte + "font-size:14px;line-height:22px;color:" + CorTexto + ";max-width:600px\">");
        html.Append("<p style=\"font-size:20px;font-weight:600;color:#0B1B36;margin:0 0 4px\">Previsio</p>");
        string periodo = "da semana";
        if (resumo.Diario)
        {
            periodo = "do dia";
        }
        html.Append("<p style=\"color:" + CorAuxiliar + ";margin:0 0 20px\">Resumo " + periodo + " · " + Html(resumo.ClienteNome) + " · " + Data(resumo.Hoje) + "</p>");
        html.Append("<p>Olá, " + Html(usuario.Nome) + ". Este é o resumo " + periodo + ".</p>");
        texto.Append("Previsio · Resumo " + periodo + " · " + resumo.ClienteNome + " · " + Data(resumo.Hoje) + "\n\n");
        texto.Append("Olá, " + usuario.Nome + ". Este é o resumo " + periodo + ".\n\n");

        // Os dois números principais
        html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:separate;border-spacing:0 0;margin:8px 0 24px\"><tr>");
        html.Append(Cartao("Próximos 7 dias", Dinheiro(resumo.Total7Dias), Quantidade(ContasEm7Dias(resumo), "conta", "contas")));
        html.Append("<td style=\"width:12px\"></td>");
        html.Append(Cartao("Próximos 30 dias", Dinheiro(resumo.Total30Dias), Quantidade(resumo.Contas30Dias, "conta", "contas")));
        html.Append("</tr></table>");
        texto.Append("Próximos 7 dias: " + Dinheiro(resumo.Total7Dias) + "\n");
        texto.Append("Próximos 30 dias: " + Dinheiro(resumo.Total30Dias) + " (" + Quantidade(resumo.Contas30Dias, "conta", "contas") + ")\n\n");

        // Vencidas em aberto: o que já passou e não foi pago (ou baixado) no Bling
        if (resumo.Vencidas > 0)
        {
            string vencidas = Quantidade(resumo.Vencidas, "conta vencida", "contas vencidas") + " em aberto no Bling, somando "
                + Dinheiro(resumo.TotalVencidas) + ". Pague ou dê baixa no Bling.";
            string link = AlertaVencimentosService.LinkVencidas(urlFront);
            html.Append("<p style=\"margin:0 0 24px;padding:12px 16px;border:1px solid #FCA5A5;border-radius:8px;background:#FEF2F2\">"
                + "<strong style=\"color:#B91C1C\">Atenção:</strong> " + Html(vencidas)
                + " <a href=\"" + Html(link) + "\" style=\"color:#B91C1C;font-weight:600\">Ver as vencidas</a></p>");
            texto.Append("ATENÇÃO: " + vencidas + "\nVer as vencidas: " + link + "\n\n");
        }

        // Vencimentos da semana, dia a dia e por empresa
        html.Append(Titulo("Vencimentos dos próximos 7 dias"));
        texto.Append("VENCIMENTOS DOS PRÓXIMOS 7 DIAS\n");
        if (resumo.Dias.Count == 0)
        {
            html.Append("<p style=\"color:" + CorSecundaria + "\">Nenhuma conta a pagar em aberto no Bling para os próximos 7 dias.</p>");
            texto.Append("Nenhuma conta a pagar em aberto no Bling para os próximos 7 dias.\n");
        }
        else
        {
            html.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;border-collapse:collapse;margin-bottom:24px\">");
            foreach (VencimentoDoDia dia in resumo.Dias)
            {
                html.Append("<tr><td style=\"padding:10px 0 4px;border-top:1px solid " + CorBorda + ";font-weight:600\">" + DiaDaSemana(dia.Data) + "</td>");
                html.Append("<td style=\"padding:10px 0 4px;border-top:1px solid " + CorBorda + ";font-weight:600;text-align:right;white-space:nowrap\">" + Dinheiro(dia.Total) + "</td></tr>");
                texto.Append(DiaDaSemana(dia.Data) + ": " + Dinheiro(dia.Total) + "\n");
                foreach (VencimentoDaEmpresa empresa in dia.Empresas)
                {
                    string descricao = empresa.Empresa + " · " + Quantidade(empresa.Quantidade, "conta", "contas");
                    html.Append("<tr><td style=\"padding:2px 0 2px 12px;color:" + CorSecundaria + "\">" + Html(descricao) + "</td>");
                    html.Append("<td style=\"padding:2px 0;color:" + CorSecundaria + ";text-align:right;white-space:nowrap\">" + Dinheiro(empresa.Valor) + "</td></tr>");
                    texto.Append("  " + descricao + ": " + Dinheiro(empresa.Valor) + "\n");
                }
            }
            html.Append("</table>");
        }
        texto.Append("\n");

        // Notas desde o último resumo
        string tituloNotas = "Notas desde " + resumo.NotasDesde.ToString("dd/MM", Brasil) + " às " + resumo.NotasDesde.ToString("HH:mm", Brasil);
        html.Append(Titulo(tituloNotas));
        texto.Append(tituloNotas.ToUpper(Brasil) + "\n");
        string recebidas;
        if (resumo.NotasRecebidas == 0)
        {
            recebidas = "Nenhuma nota chegou pelo e-mail.";
        }
        else
        {
            recebidas = Quantidade(resumo.NotasRecebidas, "nota recebida", "notas recebidas") + ", somando " + Dinheiro(resumo.ValorNotasRecebidas) + ". "
                + Quantidade(resumo.NotasLancadas, "lançada", "lançadas") + " no Bling";
            if (resumo.NotasComProblema > 0)
            {
                recebidas = recebidas + "; " + Quantidade(resumo.NotasComProblema, "com problema", "com problema") + ".";
            }
            else
            {
                recebidas = recebidas + ".";
            }
        }
        html.Append("<p style=\"margin:0 0 24px\">" + Html(recebidas) + "</p>");
        texto.Append(recebidas + "\n\n");

        // Problemas em aberto
        html.Append(Titulo("Problemas em aberto"));
        texto.Append("PROBLEMAS EM ABERTO\n");
        if (resumo.Problemas.Count == 0)
        {
            html.Append("<p style=\"margin:0 0 24px\">Nenhum. Tudo em dia.</p>");
            texto.Append("Nenhum. Tudo em dia.\n");
        }
        else
        {
            html.Append("<ul style=\"padding-left:20px;margin:0 0 24px\">");
            int mostrados = 0;
            foreach (Aviso aviso in resumo.Problemas)
            {
                if (mostrados == MaximoProblemas)
                {
                    break;
                }
                string link = urlFront + aviso.Link;
                html.Append("<li style=\"margin-bottom:8px\"><a href=\"" + Html(link) + "\" style=\"color:#1D4ED8;font-weight:600\">"
                    + Html(aviso.Titulo) + "</a><br><span style=\"color:" + CorSecundaria + "\">" + Html(aviso.Mensagem) + "</span></li>");
                texto.Append("- " + aviso.Titulo + "\n  " + aviso.Mensagem + "\n  " + link + "\n");
                mostrados++;
            }
            html.Append("</ul>");
            if (resumo.Problemas.Count > mostrados)
            {
                string mais = "E mais " + (resumo.Problemas.Count - mostrados) + " no sininho do Previsio.";
                html.Append("<p>" + mais + "</p>");
                texto.Append(mais + "\n");
            }
        }

        html.Append("<p><a href=\"" + Html(urlFront + "/pages/painel") + "\" style=\"color:#1D4ED8;font-weight:600\">Abrir o painel</a></p>");
        html.Append("<p style=\"color:" + CorAuxiliar + ";font-size:12px\">Valores das contas a pagar em aberto no Bling, sincronizadas pelo Previsio. "
            + "O dia e a hora deste e-mail são escolhidos na tela Empresas.</p></div>");
        texto.Append("\nAbrir o painel: " + urlFront + "/pages/painel\n");

        var mensagem = new MensagemEmail();
        mensagem.Para = usuario.Email;
        mensagem.NomePara = usuario.Nome;
        mensagem.Assunto = "Previsio: " + Dinheiro(resumo.Total7Dias) + " a pagar nos próximos 7 dias · " + resumo.ClienteNome;
        mensagem.Html = html.ToString();
        mensagem.Texto = texto.ToString();
        return mensagem;
    }

    public static string Dinheiro(decimal valor)
    {
        return "R$ " + valor.ToString("N2", Brasil);
    }

    private static int ContasEm7Dias(ResumoSemanal resumo)
    {
        int total = 0;
        foreach (VencimentoDoDia dia in resumo.Dias)
        {
            foreach (VencimentoDaEmpresa empresa in dia.Empresas)
            {
                total = total + empresa.Quantidade;
            }
        }
        return total;
    }

    private static string Cartao(string rotulo, string valor, string detalhe)
    {
        return "<td style=\"width:50%;padding:14px 16px;border:1px solid " + CorBorda + ";border-radius:8px;vertical-align:top\">"
            + "<div style=\"font-size:12px;color:" + CorAuxiliar + "\">" + Html(rotulo) + "</div>"
            + "<div style=\"font-size:22px;line-height:30px;font-weight:600;color:" + CorTexto + "\">" + Html(valor) + "</div>"
            + "<div style=\"font-size:12px;color:" + CorSecundaria + "\">" + Html(detalhe) + "</div></td>";
    }

    private static string Titulo(string texto)
    {
        return "<p style=\"font-size:15px;font-weight:600;margin:0 0 8px\">" + Html(texto) + "</p>";
    }

    private static string Quantidade(int quantidade, string singular, string plural)
    {
        if (quantidade == 1)
        {
            return "1 " + singular;
        }
        return quantidade + " " + plural;
    }

    private static string Data(DateTime data)
    {
        return data.ToString("dd/MM/yyyy", Brasil);
    }

    private static string DiaDaSemana(DateTime data)
    {
        return DiasCurtos[(int)data.DayOfWeek] + ", " + data.ToString("dd/MM", Brasil);
    }

    private static string Html(string texto)
    {
        return WebUtility.HtmlEncode(texto);
    }
}
