using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Jobs;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;

namespace Previsio.Api.Servicos;

/// <summary>
/// Conexão OAuth de cada empresa com a sua conta Bling.
/// </summary>
public class BlingConexaoService
{
    // O state carrega a empresa de forma assinada e vale por 30 minutos
    private static readonly TimeSpan ValidadeEstado = TimeSpan.FromMinutes(30);

    private readonly IRepositorio<Empresa> _empresas;
    private readonly AcessoEmpresaService _acesso;
    private readonly BlingClient _bling;
    private readonly TokenBlingService _tokens;
    private readonly IBackgroundJobClient _jobs;
    private readonly ITimeLimitedDataProtector _protetorEstado;
    private readonly ILogger<BlingConexaoService> _logger;

    public BlingConexaoService(
        IRepositorio<Empresa> empresas,
        AcessoEmpresaService acesso,
        BlingClient bling,
        TokenBlingService tokens,
        IBackgroundJobClient jobs,
        IDataProtectionProvider provedor,
        ILogger<BlingConexaoService> logger)
    {
        _empresas = empresas;
        _acesso = acesso;
        _bling = bling;
        _tokens = tokens;
        _jobs = jobs;
        _protetorEstado = provedor.CreateProtector("Previsio.OAuthState").ToTimeLimitedDataProtector();
        _logger = logger;
    }

    /// <summary>
    /// URL para o cliente autorizar o Previsio na conta Bling da empresa.
    /// </summary>
    public async Task<Resultado<string>> MontarUrlConexaoAsync(
        int empresaId, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Empresa? empresa = await BuscarEmpresaPermitidaAsync(empresaId, usuario, cancelamento);
        if (empresa == null)
        {
            return Resultado<string>.NaoEncontrado(EmpresasService.MensagemNaoEncontrada);
        }

        CredenciaisBling credenciais = await _tokens.CredenciaisAsync(empresa, cancelamento);
        if (!credenciais.Completas)
        {
            return Resultado<string>.Invalido("Cadastre o Client ID e o Client Secret do aplicativo do Bling desta empresa antes de conectar.");
        }

        string estado = _protetorEstado.Protect(empresaId.ToString(), ValidadeEstado);
        return Resultado<string>.Ok(_bling.MontarUrlAutorizacao(credenciais, estado));
    }

    /// <summary>
    /// Desconecta a empresa do Bling: apaga os tokens guardados.
    /// As notas dela passam a ficar como "Bling desconectado" até uma nova conexão.
    /// </summary>
    public async Task<Resultado> DesconectarAsync(int empresaId, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Empresa? empresa = await BuscarEmpresaPermitidaAsync(empresaId, usuario, cancelamento);
        if (empresa == null)
        {
            return Resultado.NaoEncontrado(EmpresasService.MensagemNaoEncontrada);
        }

        empresa.BlingAccessTokenProtegido = null;
        empresa.BlingRefreshTokenProtegido = null;
        empresa.BlingTokenExpiraEm = null;
        await _empresas.SalvarAsync(cancelamento);

        return Resultado.Ok();
    }

    /// <summary>
    /// Volta do Bling: troca o código pelo token e guarda na empresa que veio no state.
    /// Retorna um dos valores de RetornoConexaoBling.
    /// </summary>
    public async Task<string> ConcluirConexaoAsync(string? codigo, string? estado, CancellationToken cancelamento)
    {
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(estado))
        {
            return RetornoConexaoBling.Erro;
        }

        int empresaId;
        try
        {
            empresaId = int.Parse(_protetorEstado.Unprotect(estado));
        }
        catch (Exception)
        {
            return RetornoConexaoBling.Expirado;
        }

        Empresa? empresa = await _empresas.Consultar().FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return RetornoConexaoBling.Erro;
        }

        try
        {
            CredenciaisBling credenciais = await _tokens.CredenciaisAsync(empresa, cancelamento);
            TokenBling token = await _bling.TrocarCodigoPorTokenAsync(credenciais, codigo, cancelamento);
            _tokens.SalvarToken(empresa, token);
            await _empresas.SalvarAsync(cancelamento);
        }
        catch (BlingException erro)
        {
            _logger.LogError(erro, "Falha no OAuth da empresa {EmpresaId}: {Corpo}", empresaId, erro.CorpoResposta);
            return RetornoConexaoBling.Erro;
        }

        // Já busca as contas a pagar e lança notas que estavam esperando a conexão
        _jobs.Enqueue<SincronizacaoContasJob>(j => j.ExecutarAsync(CancellationToken.None));
        _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarPendentesAsync(CancellationToken.None));

        return RetornoConexaoBling.Conectado;
    }

    /// <summary>
    /// Empresa existente e acessível pelo usuário; nula nos dois casos contrários,
    /// para empresa de outro cliente responder igual a inexistente.
    /// </summary>
    private async Task<Empresa?> BuscarEmpresaPermitidaAsync(
        int empresaId, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Empresa? empresa = await _empresas.Consultar().FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return null;
        }

        bool permitido = await _acesso.PodeAcessarAsync(usuario, empresaId, cancelamento);
        if (!permitido)
        {
            return null;
        }
        return empresa;
    }
}
