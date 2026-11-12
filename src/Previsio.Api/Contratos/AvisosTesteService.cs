using System.Net;
using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Contratos;

/// <summary>
/// E-mails do teste grátis: 3 dias antes do fim e quando acaba, para os usuários da rede
/// e para os admins da Soft.IO (para ligar e fechar a venda). Cada aviso sai uma vez por prazo.
/// </summary>
public class AvisosTesteService
{
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Usuario> _usuarios;
    private readonly IEnvioEmail _email;
    private readonly IConfiguration _configuracao;
    private readonly TimeProvider _relogio;
    private readonly ILogger<AvisosTesteService> _logger;

    public AvisosTesteService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Usuario> usuarios,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<AvisosTesteService> logger)
    {
        _clientes = clientes;
        _usuarios = usuarios;
        _email = email;
        _configuracao = configuracao;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task EnviarAsync(CancellationToken cancelamento)
    {
        DateTime agora = _relogio.GetUtcNow().UtcDateTime;
        List<Cliente> emTeste = await _clientes.Consultar()
            .Where(c => c.Ativo && c.Contrato == SituacaoContrato.EmTeste && c.TesteTerminaEm != null)
            .ToListAsync(cancelamento);
        if (emTeste.Count == 0)
        {
            return;
        }

        List<Usuario> admins = await _usuarios.ConsultarSomenteLeitura()
            .Where(u => u.Ativo && u.Perfil == PerfilUsuario.Admin)
            .ToListAsync(cancelamento);

        foreach (Cliente rede in emTeste)
        {
            DateTime termino = rede.TesteTerminaEm!.Value;
            bool encerrado = termino <= agora;
            int diasRestantes = PeriodoTeste.DiasRestantes(termino, agora);

            if (encerrado && rede.TesteAvisoEncerradoEnviadoEm == null)
            {
                await AvisarAsync(rede, admins, true, 0, cancelamento);
                rede.TesteAvisoEncerradoEnviadoEm = agora;
                // Quem nem recebeu o aviso de 3 dias não recebe mais
                if (rede.TesteAvisoFinalEnviadoEm == null)
                {
                    rede.TesteAvisoFinalEnviadoEm = agora;
                }
            }
            else if (!encerrado && diasRestantes <= PeriodoTeste.DiasAvisoFinal && rede.TesteAvisoFinalEnviadoEm == null)
            {
                await AvisarAsync(rede, admins, false, diasRestantes, cancelamento);
                rede.TesteAvisoFinalEnviadoEm = agora;
            }
        }
        await _clientes.SalvarAsync(cancelamento);
    }

    private async Task AvisarAsync(Cliente rede, List<Usuario> admins, bool encerrado, int diasRestantes, CancellationToken cancelamento)
    {
        if (!_email.Configurado)
        {
            _logger.LogWarning("Smtp não configurado: aviso do teste grátis da rede {ClienteId} não enviado", rede.Id);
            return;
        }

        string ultimoDia = PeriodoTeste.UltimoDia(rede.TesteTerminaEm!.Value).ToString("dd/MM/yyyy");
        string urlFront = (_configuracao["Front:Url"] ?? "http://localhost:4200/#").TrimEnd('/');

        List<Usuario> daRede = await _usuarios.ConsultarSomenteLeitura()
            .Where(u => u.Ativo && u.Perfil == PerfilUsuario.Cliente && u.ClienteId == rede.Id)
            .ToListAsync(cancelamento);

        foreach (Usuario usuario in daRede)
        {
            string assunto;
            string texto;
            if (encerrado)
            {
                assunto = "Previsio: seu teste grátis acabou";
                texto = "O teste grátis do Previsio da " + rede.Nome + " acabou em " + ultimoDia + ". "
                    + "Paramos de ler as notas do e-mail e de lançar no Bling; os dados continuam no painel. "
                    + "Para continuar, responda este e-mail ou fale com a Soft.IO.";
            }
            else
            {
                assunto = "Previsio: " + Dias(diasRestantes) + " de teste grátis";
                texto = "O teste grátis do Previsio da " + rede.Nome + " vai até " + ultimoDia + " (" + Dias(diasRestantes) + "). "
                    + "Depois disso, o Previsio para de ler as notas do e-mail e de lançar no Bling. "
                    + "Para continuar sem interrupção, responda este e-mail ou fale com a Soft.IO.";
            }
            await EnviarAsync(usuario, assunto, texto, urlFront + "/pages/painel", cancelamento);
        }

        foreach (Usuario admin in admins)
        {
            string assunto;
            string texto;
            if (encerrado)
            {
                assunto = "Previsio: teste da " + rede.Nome + " acabou";
                texto = "O teste grátis da " + rede.Nome + " acabou em " + ultimoDia + " e o processamento parou. "
                    + "Marque como contratado ou prorrogue o teste no detalhe do cliente.";
            }
            else
            {
                assunto = "Previsio: teste da " + rede.Nome + " vai até " + ultimoDia;
                texto = "O teste grátis da " + rede.Nome + " vai até " + ultimoDia + ". "
                    + Usuarios(daRede.Count) + " Bom momento para ligar e fechar a contratação.";
            }
            await EnviarAsync(admin, assunto, texto, urlFront + "/pages/clientes/" + rede.Id, cancelamento);
        }
    }

    private async Task EnviarAsync(Usuario usuario, string assunto, string texto, string link, CancellationToken cancelamento)
    {
        var mensagem = new MensagemEmail();
        mensagem.Para = usuario.Email;
        mensagem.NomePara = usuario.Nome;
        mensagem.Assunto = assunto;
        mensagem.Texto = "Olá, " + usuario.Nome + ".\n\n" + texto + "\n\n" + link;
        mensagem.Html = "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#0F172A;max-width:560px\">"
            + "<p style=\"font-size:20px;font-weight:600;color:#0B1B36;margin:0 0 16px\">Previsio</p>"
            + "<p>Olá, " + WebUtility.HtmlEncode(usuario.Nome) + ".</p>"
            + "<p>" + WebUtility.HtmlEncode(texto) + "</p>"
            + "<p><a href=\"" + WebUtility.HtmlEncode(link) + "\" style=\"color:#1D4ED8;font-weight:600\">Abrir o Previsio</a></p></div>";
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
            _logger.LogError(erro, "Falha ao enviar o aviso do teste grátis ao usuário {UsuarioId}", usuario.Id);
        }
    }

    private static string Dias(int dias)
    {
        if (dias == 1)
        {
            return "último dia";
        }
        return "faltam " + dias + " dias";
    }

    private static string Usuarios(int quantidade)
    {
        if (quantidade == 0)
        {
            return "A rede ainda não tem usuário ativo.";
        }
        if (quantidade == 1)
        {
            return "A rede tem 1 usuário ativo.";
        }
        return "A rede tem " + quantidade + " usuários ativos.";
    }
}
