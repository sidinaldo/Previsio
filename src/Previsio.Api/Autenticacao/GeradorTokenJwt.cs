using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Previsio.Core.Entidades;

namespace Previsio.Api.Autenticacao;

/// <summary>
/// Emite o token de acesso (JWT assinado com HS256) de um usuário.
/// </summary>
public class GeradorTokenJwt
{
    private readonly JwtOptions _opcoes;
    private readonly SigningCredentials _credenciais;

    public GeradorTokenJwt(IOptions<JwtOptions> opcoes)
    {
        _opcoes = opcoes.Value;
        _credenciais = new SigningCredentials(CriarChave(_opcoes.Chave), SecurityAlgorithms.HmacSha256);
    }

    public static SymmetricSecurityKey CriarChave(string chave)
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave));
    }

    public TokenGerado Gerar(Usuario usuario)
    {
        DateTime agora = DateTime.UtcNow;
        DateTime expiraEm = agora.AddMinutes(_opcoes.ValidadeMinutos);

        var claims = new Dictionary<string, object>();
        claims[ClaimsPrevisio.UsuarioId] = usuario.Id.ToString(CultureInfo.InvariantCulture);
        claims[ClaimsPrevisio.Nome] = usuario.Nome;
        claims[ClaimsPrevisio.Email] = usuario.Email;
        claims[ClaimsPrevisio.Perfil] = Perfis.Nome(usuario.Perfil);
        claims[ClaimsPrevisio.Carimbo] = usuario.CarimboSeguranca;
        claims[ClaimsPrevisio.IdToken] = Guid.NewGuid().ToString("N");
        if (usuario.ClienteId.HasValue)
        {
            claims[ClaimsPrevisio.ClienteId] = usuario.ClienteId.Value.ToString(CultureInfo.InvariantCulture);
        }

        var descritor = new SecurityTokenDescriptor();
        descritor.Issuer = _opcoes.Emissor;
        descritor.Audience = _opcoes.Audiencia;
        descritor.IssuedAt = agora;
        descritor.NotBefore = agora;
        descritor.Expires = expiraEm;
        descritor.Claims = claims;
        descritor.SigningCredentials = _credenciais;

        var resultado = new TokenGerado();
        resultado.Token = new JsonWebTokenHandler().CreateToken(descritor);
        resultado.ExpiraEm = expiraEm;
        return resultado;
    }
}
