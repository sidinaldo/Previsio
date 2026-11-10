using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Jobs;
using Previsio.Api.Servicos;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Controllers;

/// <summary>
/// Conexão OAuth de cada empresa com a sua conta Bling.
/// </summary>
[ApiController]
public class BlingController : ControllerBase
{
    private readonly PrevisioDbContext _db;
    private readonly BlingClient _bling;
    private readonly TokenBlingService _tokens;
    private readonly IConfiguration _configuracao;
    private readonly IBackgroundJobClient _jobs;
    private readonly ITimeLimitedDataProtector _protetorEstado;
    private readonly ILogger<BlingController> _logger;

    public BlingController(
        PrevisioDbContext db,
        BlingClient bling,
        TokenBlingService tokens,
        IConfiguration configuracao,
        IBackgroundJobClient jobs,
        IDataProtectionProvider provedor,
        ILogger<BlingController> logger)
    {
        _db = db;
        _bling = bling;
        _tokens = tokens;
        _configuracao = configuracao;
        _jobs = jobs;
        _protetorEstado = provedor.CreateProtector("Previsio.OAuthState").ToTimeLimitedDataProtector();
        _logger = logger;
    }

    /// <summary>
    /// Retorna a URL para o cliente autorizar o Previsio na conta Bling da empresa.
    /// </summary>
    [HttpGet("api/empresas/{empresaId:int}/bling/conectar")]
    public async Task<IActionResult> Conectar(int empresaId, CancellationToken cancelamento)
    {
        Empresa? empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return NotFound("Empresa não encontrada.");
        }

        // Empresa de outro cliente responde igual a inexistente, para não revelar ids
        bool permitido = await ControleAcesso.PodeAcessarEmpresaAsync(_db, User, empresaId, cancelamento);
        if (!permitido)
        {
            return NotFound("Empresa não encontrada.");
        }

        CredenciaisBling credenciais = _tokens.Credenciais(empresa);
        if (!credenciais.Completas)
        {
            return BadRequest("Cadastre o Client ID e o Client Secret do aplicativo do Bling desta empresa antes de conectar.");
        }

        // O state carrega a empresa de forma assinada e vale por 30 minutos
        string estado = _protetorEstado.Protect(empresaId.ToString(), TimeSpan.FromMinutes(30));
        string url = _bling.MontarUrlAutorizacao(credenciais, estado);
        return Ok(new { url = url });
    }

    /// <summary>
    /// Desconecta a empresa do Bling: apaga os tokens guardados.
    /// As notas dela passam a ficar como "Bling desconectado" até uma nova conexão.
    /// </summary>
    [HttpDelete("api/empresas/{empresaId:int}/bling")]
    public async Task<IActionResult> Desconectar(int empresaId, CancellationToken cancelamento)
    {
        Empresa? empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return NotFound("Empresa não encontrada.");
        }

        // Empresa de outro cliente responde igual a inexistente, para não revelar ids
        bool permitido = await ControleAcesso.PodeAcessarEmpresaAsync(_db, User, empresaId, cancelamento);
        if (!permitido)
        {
            return NotFound("Empresa não encontrada.");
        }

        empresa.BlingAccessTokenProtegido = null;
        empresa.BlingRefreshTokenProtegido = null;
        empresa.BlingTokenExpiraEm = null;
        await _db.SaveChangesAsync(cancelamento);

        return NoContent();
    }

    /// <summary>
    /// Link de redirecionamento cadastrado no app do Bling.
    /// Fica sem login porque quem chama é o navegador vindo do Bling; a empresa
    /// vem no state assinado, gerado só depois da verificação de acesso em Conectar.
    /// </summary>
    [HttpGet("api/bling/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, CancellationToken cancelamento)
    {
        string urlFront = _configuracao["Front:Url"] ?? "http://localhost:4200";

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Redirect(urlFront + "/pages/empresas?bling=erro");
        }

        int empresaId;
        try
        {
            empresaId = int.Parse(_protetorEstado.Unprotect(state));
        }
        catch (Exception)
        {
            return Redirect(urlFront + "/pages/empresas?bling=expirado");
        }

        Empresa? empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return Redirect(urlFront + "/pages/empresas?bling=erro");
        }

        try
        {
            TokenBling token = await _bling.TrocarCodigoPorTokenAsync(_tokens.Credenciais(empresa), code, cancelamento);
            _tokens.SalvarToken(empresa, token);
            await _db.SaveChangesAsync(cancelamento);
        }
        catch (BlingException erro)
        {
            _logger.LogError(erro, "Falha no OAuth da empresa {EmpresaId}: {Corpo}", empresaId, erro.CorpoResposta);
            return Redirect(urlFront + "/pages/empresas?bling=erro");
        }

        // Já busca as contas a pagar e lança notas que estavam esperando a conexão
        _jobs.Enqueue<SincronizacaoContasJob>(j => j.ExecutarAsync(CancellationToken.None));
        _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarPendentesAsync(CancellationToken.None));

        return Redirect(urlFront + "/pages/empresas?bling=conectado");
    }
}
