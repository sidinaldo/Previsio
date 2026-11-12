using Microsoft.AspNetCore.Mvc;
using Previsio.Api.Controllers;
using Previsio.Api.Jobs;
using Previsio.Tests.Apoio;
using Xunit;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Tests.Api.Controllers;

public class JobsControllerTests
{
    /// <summary>
    /// Responde "em execução" só para o job e o método configurados, e guarda o que foi perguntado.
    /// </summary>
    private class ExecucaoDeTeste : IExecucaoJobs
    {
        public Type? TipoRodando { get; set; }
        public string MetodoRodando { get; set; } = "";
        public List<string> Perguntas { get; } = new List<string>();

        public bool EstaEmExecucao(Type tipoJob, string metodo)
        {
            Perguntas.Add(tipoJob.Name + "." + metodo);
            return tipoJob == TipoRodando && metodo == MetodoRodando;
        }
    }

    private static JobsController CriarController(ExecucaoDeTeste execucao)
    {
        var controller = new JobsController(new FilaDeJobsDeTeste(), execucao);
        controller.ControllerContext = UsuarioDeTeste.Admin();
        return controller;
    }

    [Fact]
    public void Situacao_DizSeALeituraDeEmailAindaEstaRodando()
    {
        var execucao = new ExecucaoDeTeste();
        execucao.TipoRodando = typeof(LeituraEmailJob);
        execucao.MetodoRodando = nameof(LeituraEmailJob.ExecutarAsync);
        JobsController controller = CriarController(execucao);

        ActionResult<SituacaoJobDto> leitura = controller.Situacao("leitura-email");
        ActionResult<SituacaoJobDto> sincronizacao = controller.Situacao("sincronizacao");

        Assert.True(leitura.Value!.EmExecucao);
        Assert.Equal("leitura-email", leitura.Value.Nome);
        Assert.False(sincronizacao.Value!.EmExecucao);
        Assert.Equal(new[] { "LeituraEmailJob.ExecutarAsync", "SincronizacaoContasJob.ExecutarAsync" }, execucao.Perguntas.ToArray());
    }

    [Fact]
    public void Situacao_DeJobDesconhecido_Responde404()
    {
        ActionResult<SituacaoJobDto> resposta = CriarController(new ExecucaoDeTeste()).Situacao("backup");

        Assert.IsType<NotFoundObjectResult>(resposta.Result);
    }

    [Fact]
    public void Executar_ColocaOJobNaFila()
    {
        var fila = new FilaDeJobsDeTeste();
        var controller = new JobsController(fila, new ExecucaoDeTeste());
        controller.ControllerContext = UsuarioDeTeste.Admin();

        IActionResult resposta = controller.ExecutarJob("leitura-email");

        Assert.IsType<AcceptedResult>(resposta);
        Assert.Single(fila.Enfileirados);
        Assert.Equal(typeof(LeituraEmailJob), fila.Enfileirados[0].Type);
    }
}
