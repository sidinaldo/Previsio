using Previsio.Api.Excecoes;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Servicos;

/// <summary>
/// Entrega um access token válido para a conta Bling de uma empresa,
/// renovando com o refresh token quando estiver perto de expirar.
/// </summary>
public class TokenBlingService
{
    private static readonly TimeSpan MargemRenovacao = TimeSpan.FromMinutes(5);

    private readonly BlingClient _bling;
    private readonly PrevisioDbContext _db;
    private readonly ProtetorSegredos _protetor;
    private readonly ILogger<TokenBlingService> _logger;

    public TokenBlingService(
        BlingClient bling,
        PrevisioDbContext db,
        ProtetorSegredos protetor,
        ILogger<TokenBlingService> logger)
    {
        _bling = bling;
        _db = db;
        _protetor = protetor;
        _logger = logger;
    }

    public async Task<string> ObterAccessTokenAsync(Empresa empresa, CancellationToken cancelamento)
    {
        if (!empresa.BlingConectado)
        {
            throw new EmpresaDesconectadaException("A empresa " + empresa.Nome + " não está conectada ao Bling.");
        }

        bool tokenValido = empresa.BlingAccessTokenProtegido != null
            && empresa.BlingTokenExpiraEm.HasValue
            && empresa.BlingTokenExpiraEm.Value > DateTime.UtcNow.Add(MargemRenovacao);

        if (tokenValido)
        {
            return _protetor.Revelar(empresa.BlingAccessTokenProtegido!);
        }

        string refreshToken = _protetor.Revelar(empresa.BlingRefreshTokenProtegido!);

        TokenBling novoToken;
        try
        {
            novoToken = await _bling.RenovarTokenAsync(refreshToken, cancelamento);
        }
        catch (BlingException erro)
        {
            _logger.LogError(erro, "Não foi possível renovar o token da empresa {EmpresaId}. Corpo: {Corpo}",
                empresa.Id, erro.CorpoResposta);

            // Refresh token revogado ou expirado: o cliente precisa reconectar
            empresa.BlingAccessTokenProtegido = null;
            empresa.BlingRefreshTokenProtegido = null;
            empresa.BlingTokenExpiraEm = null;
            await _db.SaveChangesAsync(cancelamento);

            throw new EmpresaDesconectadaException("A conexão da empresa " + empresa.Nome + " com o Bling expirou. Reconecte.");
        }

        SalvarToken(empresa, novoToken);
        await _db.SaveChangesAsync(cancelamento);

        return novoToken.AccessToken;
    }

    public void SalvarToken(Empresa empresa, TokenBling token)
    {
        empresa.BlingAccessTokenProtegido = _protetor.Proteger(token.AccessToken);
        empresa.BlingRefreshTokenProtegido = _protetor.Proteger(token.RefreshToken);
        empresa.BlingTokenExpiraEm = DateTime.UtcNow.AddSeconds(token.ExpiraEmSegundos);
    }

    public static string IdentificadorConta(Empresa empresa)
    {
        return "empresa-" + empresa.Id;
    }
}
