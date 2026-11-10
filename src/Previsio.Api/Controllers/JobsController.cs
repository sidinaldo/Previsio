using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Autenticacao;
using Previsio.Api.Jobs;

namespace Previsio.Api.Controllers;

[ApiController]
public class JobsController : ControllerBase
{
    private readonly IBackgroundJobClient _jobs;

    public JobsController(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    /// <summary>
    /// Dispara manualmente um dos jobs (útil em testes e no piloto).
    /// Só administrador: cada job percorre todos os clientes.
    /// </summary>
    [HttpPost("api/jobs/{nome}/executar")]
    [Authorize(Roles = Perfis.Admin)]
    public IActionResult ExecutarJob(string nome)
    {
        if (nome == "leitura-email")
        {
            _jobs.Enqueue<LeituraEmailJob>(j => j.ExecutarAsync(CancellationToken.None));
        }
        else if (nome == "lancamento")
        {
            _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarPendentesAsync(CancellationToken.None));
        }
        else if (nome == "sincronizacao")
        {
            _jobs.Enqueue<SincronizacaoContasJob>(j => j.ExecutarAsync(CancellationToken.None));
        }
        else
        {
            return NotFound("Use: leitura-email, lancamento ou sincronizacao.");
        }
        return Accepted();
    }
}
