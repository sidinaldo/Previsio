using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Email;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// "Esqueci minha senha": envia por e-mail um link que vale 1 hora e serve uma vez só.
/// O pedido nunca diz se o e-mail está cadastrado, para não revelar quem usa o Previsio.
/// </summary>
public class RedefinicaoSenhaService
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(1);

    public const string MensagemLinkInvalido = "Este link de redefinição é inválido ou expirou. Peça um novo em \"Esqueci minha senha\".";

    private readonly PrevisioDbContext _db;
    private readonly AutenticacaoService _autenticacao;
    private readonly IEnvioEmail _email;
    private readonly IConfiguration _configuracao;
    private readonly IHostEnvironment _ambiente;
    private readonly TimeProvider _relogio;
    private readonly ILogger<RedefinicaoSenhaService> _logger;

    public RedefinicaoSenhaService(
        PrevisioDbContext db,
        AutenticacaoService autenticacao,
        IEnvioEmail email,
        IConfiguration configuracao,
        IHostEnvironment ambiente,
        TimeProvider relogio,
        ILogger<RedefinicaoSenhaService> logger)
    {
        _db = db;
        _autenticacao = autenticacao;
        _email = email;
        _configuracao = configuracao;
        _ambiente = ambiente;
        _relogio = relogio;
        _logger = logger;
    }

    /// <summary>
    /// Gera o link e envia por e-mail, se o usuário existir e estiver ativo.
    /// Para quem pediu a resposta é sempre a mesma; falha no envio só vai para o log.
    /// </summary>
    public async Task SolicitarAsync(string? email, CancellationToken cancelamento)
    {
        string emailNormalizado = RegrasUsuario.NormalizarEmail(email);
        if (!RegrasUsuario.EmailValido(emailNormalizado))
        {
            return;
        }

        Usuario? usuario = await _db.Usuarios
            .Include(u => u.Cliente)
            .FirstOrDefaultAsync(u => u.Email == emailNormalizado, cancelamento);
        if (usuario == null || !usuario.Ativo || (usuario.Cliente != null && !usuario.Cliente.Ativo))
        {
            _logger.LogInformation("Redefinição de senha pedida para e-mail sem acesso ativo");
            return;
        }

        string token = GerarToken();
        usuario.RedefinicaoSenhaHash = Hash(token);
        usuario.RedefinicaoSenhaExpiraEm = _relogio.GetUtcNow().UtcDateTime.Add(Validade);
        await _db.SaveChangesAsync(cancelamento);

        string link = MontarLink(token);
        if (!_email.Configurado)
        {
            if (_ambiente.IsDevelopment())
            {
                // Sem SMTP no ambiente de desenvolvimento: o link vai para o console para dar para testar
                _logger.LogWarning("Smtp não configurado. Link de redefinição do usuário {UsuarioId}: {Link}", usuario.Id, link);
            }
            else
            {
                _logger.LogError("Smtp não configurado: não foi possível enviar a redefinição de senha do usuário {UsuarioId}", usuario.Id);
            }
            return;
        }

        try
        {
            await _email.EnviarAsync(MontarEmail(usuario, link), cancelamento);
            _logger.LogInformation("Link de redefinição de senha enviado ao usuário {UsuarioId}", usuario.Id);
        }
        catch (OperationCanceledException erro)
        {
            // Quem pediu desistiu (a requisição caiu): não é falha do servidor de e-mail
            if (cancelamento.IsCancellationRequested)
            {
                throw;
            }
            _logger.LogError(erro, "O servidor de e-mail não respondeu ao enviar a redefinição de senha do usuário {UsuarioId}", usuario.Id);
        }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha ao enviar o e-mail de redefinição de senha do usuário {UsuarioId}", usuario.Id);
        }
    }

    /// <summary>
    /// Troca a senha pelo token do link. O link deixa de valer e as sessões abertas caem.
    /// </summary>
    public async Task<Resultado> RedefinirAsync(string? token, string? novaSenha, CancellationToken cancelamento)
    {
        string tokenInformado = (token ?? "").Trim();
        if (tokenInformado == "" || tokenInformado.Length > 200)
        {
            return Resultado.Invalido(MensagemLinkInvalido);
        }

        string? erroSenha = RegrasUsuario.ValidarSenha(novaSenha);
        if (erroSenha != null)
        {
            return Resultado.Invalido(erroSenha);
        }

        string hash = Hash(tokenInformado);
        Usuario? usuario = await _db.Usuarios
            .Include(u => u.Cliente)
            .FirstOrDefaultAsync(u => u.RedefinicaoSenhaHash == hash, cancelamento);

        DateTime agora = _relogio.GetUtcNow().UtcDateTime;
        if (usuario == null
            || usuario.RedefinicaoSenhaExpiraEm == null
            || usuario.RedefinicaoSenhaExpiraEm.Value <= agora
            || !usuario.Ativo
            || (usuario.Cliente != null && !usuario.Cliente.Ativo))
        {
            return Resultado.Invalido(MensagemLinkInvalido);
        }

        _autenticacao.DefinirSenha(usuario, novaSenha!);
        usuario.RedefinicaoSenhaHash = null;
        usuario.RedefinicaoSenhaExpiraEm = null;
        await _db.SaveChangesAsync(cancelamento);

        _logger.LogInformation("Senha redefinida pelo link de e-mail para o usuário {UsuarioId}", usuario.Id);
        return Resultado.Ok();
    }

    /// <summary>
    /// 32 bytes aleatórios em base64 sem os caracteres que atrapalham numa URL.
    /// </summary>
    private static string GerarToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string MontarLink(string token)
    {
        string urlFront = (_configuracao["Front:Url"] ?? "http://localhost:4200/#").TrimEnd('/');
        return urlFront + "/pages/redefinir-senha?token=" + Uri.EscapeDataString(token);
    }

    private static MensagemEmail MontarEmail(Usuario usuario, string link)
    {
        string nome = WebUtility.HtmlEncode(usuario.Nome);
        string linkHtml = WebUtility.HtmlEncode(link);

        var mensagem = new MensagemEmail();
        mensagem.Para = usuario.Email;
        mensagem.NomePara = usuario.Nome;
        mensagem.Assunto = "Redefinir sua senha do Previsio";
        mensagem.Html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:22px;color:#0F172A;max-width:520px\">"
            + "<p style=\"font-size:20px;font-weight:600;color:#0B1B36;margin:0 0 16px\">Previsio</p>"
            + "<p>Olá, " + nome + ".</p>"
            + "<p>Recebemos um pedido para redefinir a sua senha do Previsio. Clique no botão para criar uma nova:</p>"
            + "<p style=\"margin:24px 0\"><a href=\"" + linkHtml + "\" style=\"background:#1D4ED8;color:#FFFFFF;"
            + "text-decoration:none;padding:10px 18px;border-radius:6px;font-weight:600;display:inline-block\">Criar nova senha</a></p>"
            + "<p style=\"color:#475569\">O link vale por 1 hora e só pode ser usado uma vez. "
            + "Se não foi você que pediu, ignore este e-mail: sua senha continua a mesma.</p>"
            + "<p style=\"color:#5E6E85;font-size:12px;word-break:break-all\">Se o botão não abrir, copie este endereço: " + linkHtml + "</p>"
            + "</div>";
        mensagem.Texto =
            "Olá, " + usuario.Nome + ".\n\n"
            + "Recebemos um pedido para redefinir a sua senha do Previsio. Abra o link para criar uma nova:\n"
            + link + "\n\n"
            + "O link vale por 1 hora e só pode ser usado uma vez. Se não foi você que pediu, ignore este e-mail.";
        return mensagem;
    }
}
