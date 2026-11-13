using Hangfire.Common;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.Sincronizacao;

public class SincronizacaoManualServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly FilaDeJobsDeTeste _fila = new FilaDeJobsDeTeste();
    // 11/11/2026 12h UTC
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private readonly DateTime _agora = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private SincronizacaoManualService CriarServico()
    {
        return new SincronizacaoManualService(new Repositorio<Empresa>(_db), _fila, _relogio);
    }

    private async Task<int> CadastrarRede(DateTime? ultimaSincronizacao, bool conectada = true)
    {
        var rede = new Cliente { Nome = "Rede" };
        _db.Clientes.Add(rede);
        await _db.SaveChangesAsync();
        var empresa = new Empresa { ClienteId = rede.Id, Nome = "Loja", Cnpj = "11222333000181", UltimaSincronizacaoEm = ultimaSincronizacao };
        if (conectada)
        {
            empresa.BlingRefreshTokenProtegido = "x";
        }
        _db.Empresas.Add(empresa);
        await _db.SaveChangesAsync();
        return rede.Id;
    }

    [Fact]
    public async Task Enfileira_a_sincronizacao_so_da_rede()
    {
        int rede = await CadastrarRede(_agora.AddMinutes(-10));

        Resultado<SituacaoSincronizacaoDto> resultado = await CriarServico().SolicitarAsync(rede, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor!.EmExecucao);
        Job job = Assert.Single(_fila.Enfileirados);
        Assert.Equal("ExecutarAsync", job.Method.Name);
        Assert.Equal(rede, job.Args[0]);
    }

    [Fact]
    public async Task Recusa_se_sincronizou_ha_menos_de_2_minutos()
    {
        int rede = await CadastrarRede(_agora.AddSeconds(-30));

        Resultado<SituacaoSincronizacaoDto> resultado = await CriarServico().SolicitarAsync(rede, CancellationToken.None);

        Assert.Equal(TipoResultado.Conflito, resultado.Tipo);
        Assert.Contains("90 segundos", resultado.Mensagem);
        Assert.Empty(_fila.Enfileirados);
    }

    [Fact]
    public async Task Com_uma_sincronizacao_em_andamento_so_acompanha()
    {
        int rede = await CadastrarRede(_agora.AddSeconds(-30));
        _fila.EmAndamento = true;

        Resultado<SituacaoSincronizacaoDto> resultado = await CriarServico().SolicitarAsync(rede, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor!.EmExecucao);
        Assert.Empty(_fila.Enfileirados);
    }

    [Fact]
    public async Task Sem_empresa_conectada_nao_sincroniza()
    {
        int rede = await CadastrarRede(null, false);

        Resultado<SituacaoSincronizacaoDto> resultado = await CriarServico().SolicitarAsync(rede, CancellationToken.None);

        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
        Assert.Empty(_fila.Enfileirados);
    }

    [Fact]
    public async Task Situacao_traz_a_ultima_sincronizacao_e_quando_pode_de_novo()
    {
        int rede = await CadastrarRede(_agora.AddMinutes(-1));

        SituacaoSincronizacaoDto situacao = await CriarServico().SituacaoAsync(rede, CancellationToken.None);

        Assert.False(situacao.EmExecucao);
        Assert.Equal(_agora.AddMinutes(-1), situacao.UltimaSincronizacaoEm);
        Assert.Equal(_agora.AddMinutes(1), situacao.PodeSincronizarEm);
    }
}
