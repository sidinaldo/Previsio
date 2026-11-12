using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Controllers;

/// <summary>
/// Login e dados do próprio usuário.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AutenticacaoController : PrevisioControllerBase
{
    // Mesma resposta exista ou não o e-mail: não revela quem tem acesso
    public const string MensagemPedidoRedefinicao =
        "Se o e-mail estiver cadastrado, enviamos um link para criar uma nova senha. Ele vale por 1 hora.";

    private readonly AutenticacaoService _autenticacao;
    private readonly RedefinicaoSenhaService _redefinicao;
    private readonly GeradorTokenJwt _gerador;

    public AutenticacaoController(
        AutenticacaoService autenticacao, RedefinicaoSenhaService redefinicao, GeradorTokenJwt gerador)
    {
        _autenticacao = autenticacao;
        _redefinicao = redefinicao;
        _gerador = gerador;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    public async Task<ActionResult<RespostaLoginDto>> Login(LoginDto dados, CancellationToken cancelamento)
    {
        Usuario? usuario = await _autenticacao.AutenticarAsync(dados.Email, dados.Senha, cancelamento);
        if (usuario == null)
        {
            return Unauthorized("E-mail ou senha inválidos.");
        }

        return MontarResposta(usuario);
    }

    [HttpGet("eu")]
    public async Task<ActionResult<UsuarioDto>> Eu(CancellationToken cancelamento)
    {
        Usuario? usuario = await BuscarUsuarioLogadoAsync(cancelamento);
        if (usuario == null)
        {
            return Unauthorized();
        }

        return UsuarioMapeamento.ParaDto(usuario);
    }

    /// <summary>
    /// Troca a senha do próprio usuário. Os outros tokens dele deixam de valer
    /// e a resposta traz um token novo para continuar usando.
    /// </summary>
    [HttpPost("trocar-senha")]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    public async Task<ActionResult<RespostaLoginDto>> TrocarSenha(TrocaSenhaDto dados, CancellationToken cancelamento)
    {
        Usuario? usuario = await BuscarUsuarioLogadoAsync(cancelamento);
        if (usuario == null)
        {
            return Unauthorized();
        }

        Resultado resultado = await _autenticacao.TrocarSenhaAsync(usuario, dados.SenhaAtual, dados.NovaSenha, cancelamento);
        if (!resultado.Sucesso)
        {
            return BadRequest(resultado.Mensagem);
        }

        return MontarResposta(usuario);
    }

    /// <summary>
    /// "Esqueci minha senha": envia o link por e-mail. Responde sempre igual.
    /// </summary>
    [HttpPost("esqueci-senha")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    public async Task<ActionResult<string>> EsqueciSenha(EsqueciSenhaDto dados, CancellationToken cancelamento)
    {
        await _redefinicao.SolicitarAsync(dados.Email, cancelamento);
        return MensagemPedidoRedefinicao;
    }

    /// <summary>
    /// Nova senha pelo link do e-mail. As sessões abertas do usuário caem; ele entra de novo pelo login.
    /// </summary>
    [HttpPost("redefinir-senha")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitesRequisicao.Login)]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaDto dados, CancellationToken cancelamento)
    {
        return Responder(await _redefinicao.RedefinirAsync(dados.Token, dados.NovaSenha, cancelamento), () => NoContent());
    }

    private async Task<Usuario?> BuscarUsuarioLogadoAsync(CancellationToken cancelamento)
    {
        int? usuarioId = User.UsuarioId();
        if (!usuarioId.HasValue)
        {
            return null;
        }

        return await _autenticacao.ObterUsuarioAsync(usuarioId.Value, cancelamento);
    }

    private RespostaLoginDto MontarResposta(Usuario usuario)
    {
        TokenGerado token = _gerador.Gerar(usuario);

        var resposta = new RespostaLoginDto();
        resposta.Token = token.Token;
        resposta.ExpiraEm = token.ExpiraEm;
        resposta.Usuario = UsuarioMapeamento.ParaDto(usuario);
        return resposta;
    }
}
