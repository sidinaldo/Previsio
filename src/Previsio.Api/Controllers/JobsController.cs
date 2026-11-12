using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Jobs;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Controllers;

[ApiController]
public class JobsController : ControllerBase
{
    private const string NomesValidos = "Use: leitura-email, lancamento ou sincronizacao.";

    private readonly IBackgroundJobClient _jobs;
    private readonly IExecucaoJobs _execucao;

    public JobsController(IBackgroundJobClient jobs, IExecucaoJobs execucao)
    {
        _jobs = jobs;
        _execucao = execucao;
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
            return NotFound(NomesValidos);
        }
        return Accepted();
    }

    /// <summary>
    /// Se o job ainda está na fila ou rodando. A tela consulta depois de "executar" para
    /// atualizar sozinha quando ele terminar.
    /// </summary>
    [HttpGet("api/jobs/{nome}/situacao")]
    [Authorize(Roles = Perfis.Admin)]
    public ActionResult<SituacaoJobDto> Situacao(string nome)
    {
        Type tipo;
        string metodo;
        if (nome == "leitura-email")
        {
            tipo = typeof(LeituraEmailJob);
            metodo = nameof(LeituraEmailJob.ExecutarAsync);
        }
        else if (nome == "lancamento")
        {
            tipo = typeof(LancamentoNotaJob);
            metodo = nameof(LancamentoNotaJob.LancarPendentesAsync);
        }
        else if (nome == "sincronizacao")
        {
            tipo = typeof(SincronizacaoContasJob);
            metodo = nameof(SincronizacaoContasJob.ExecutarAsync);
        }
        else
        {
            return NotFound(NomesValidos);
        }

        var dto = new SituacaoJobDto();
        dto.Nome = nome;
        dto.EmExecucao = _execucao.EstaEmExecucao(tipo, metodo);
        return dto;
    }
}
