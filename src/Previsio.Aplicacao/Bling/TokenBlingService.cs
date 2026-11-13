using Microsoft.EntityFrameworkCore;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Aplicacao.Bling;

/// <summary>
/// Entrega um access token válido para a conta Bling de uma empresa,
/// renovando com o refresh token quando estiver perto de expirar.
/// </summary>
public class TokenBlingService
{
    private static readonly TimeSpan MargemRenovacao = TimeSpan.FromMinutes(5);

    private readonly BlingClient _bling;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IProtetorSegredos _protetor;
    private readonly ILogger<TokenBlingService> _logger;

    public TokenBlingService(
        BlingClient bling,
        IRepositorio<Cliente> clientes,
        IProtetorSegredos protetor,
        ILogger<TokenBlingService> logger)
    {
        _bling = bling;
        _clientes = clientes;
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
            CredenciaisBling credenciais = await CredenciaisAsync(empresa, cancelamento);
            novoToken = await _bling.RenovarTokenAsync(credenciais, refreshToken, cancelamento);
        }
        catch (BlingException erro)
        {
            _logger.LogError(erro, "Não foi possível renovar o token da empresa {EmpresaId}. Corpo: {Corpo}",
                empresa.Id, erro.CorpoResposta);

            // Sem resposta do Bling (rede, tempo esgotado) não é recusa do token:
            // mantém a conexão e deixa quem chamou registrar a falha e tentar de novo depois
            if (erro.FalhaDeConexao)
            {
                throw;
            }

            // Refresh token revogado ou expirado: o cliente precisa reconectar
            empresa.BlingAccessTokenProtegido = null;
            empresa.BlingRefreshTokenProtegido = null;
            empresa.BlingTokenExpiraEm = null;
            await _clientes.SalvarAsync(cancelamento);

            throw new EmpresaDesconectadaException(
                "A conexão da empresa " + empresa.Nome + " com o Bling expirou. Reconecte.", erro);
        }

        SalvarToken(empresa, novoToken);
        await _clientes.SalvarAsync(cancelamento);

        return novoToken.AccessToken;
    }

    /// <summary>
    /// O Bling recusou um access token que ainda não tinha vencido (acontece quando os escopos
    /// do aplicativo mudam: o Bling revoga os tokens na hora). Descarta o token e renova com o
    /// refresh token. Se a renovação também for recusada, a empresa fica desconectada e sai
    /// EmpresaDesconectadaException, como numa renovação comum.
    /// </summary>
    public Task<string> RenovarAposRecusaAsync(Empresa empresa, CancellationToken cancelamento)
    {
        _logger.LogWarning("O Bling recusou o token da empresa {EmpresaId} antes de vencer; renovando", empresa.Id);
        empresa.BlingTokenExpiraEm = null;
        return ObterAccessTokenAsync(empresa, cancelamento);
    }

    /// <summary>
    /// Aplicativo do Bling usado pela empresa, nesta ordem: o privado dela, o da rede (Cliente)
    /// e, se nenhum dos dois estiver cadastrado, o aplicativo global do Previsio.
    /// </summary>
    public async Task<CredenciaisBling> CredenciaisAsync(Empresa empresa, CancellationToken cancelamento)
    {
        if (!string.IsNullOrWhiteSpace(empresa.BlingClientId))
        {
            return Montar(empresa.BlingClientId, empresa.BlingClientSecretProtegido);
        }

        Cliente? rede = empresa.Cliente;
        if (rede == null)
        {
            rede = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == empresa.ClienteId, cancelamento);
        }
        if (rede != null && !string.IsNullOrWhiteSpace(rede.BlingClientId))
        {
            return Montar(rede.BlingClientId, rede.BlingClientSecretProtegido);
        }

        return _bling.CredenciaisPadrao;
    }

    private CredenciaisBling Montar(string clientId, string? secretProtegido)
    {
        var credenciais = new CredenciaisBling();
        credenciais.ClientId = clientId;
        if (!string.IsNullOrEmpty(secretProtegido))
        {
            credenciais.ClientSecret = _protetor.Revelar(secretProtegido);
        }
        return credenciais;
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
