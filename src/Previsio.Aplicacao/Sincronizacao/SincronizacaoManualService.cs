using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Sincronizacao;

/// <summary>
/// "Sincronizar com o Bling" pedido pela tela: traz as contas só das empresas da rede, com um
/// intervalo mínimo entre uma sincronização e outra para não estourar o limite do Bling.
/// </summary>
public class SincronizacaoManualService
{
    // Fixo: tempo mínimo desde a última sincronização da rede (automática ou pedida)
    public static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMinutes(2);

    public const string MensagemSemEmpresa = "Nenhuma empresa desta rede está conectada ao Bling.";

    private readonly IRepositorio<Empresa> _empresas;
    private readonly IFilaJobs _fila;
    private readonly TimeProvider _relogio;

    public SincronizacaoManualService(IRepositorio<Empresa> empresas, IFilaJobs fila, TimeProvider relogio)
    {
        _empresas = empresas;
        _fila = fila;
        _relogio = relogio;
    }

    public async Task<SituacaoSincronizacaoDto> SituacaoAsync(int clienteId, CancellationToken cancelamento)
    {
        List<DateTime?> datas = await EmpresasConectadas(clienteId)
            .Select(e => e.UltimaSincronizacaoEm)
            .ToListAsync(cancelamento);
        return Montar(datas);
    }

    /// <summary>
    /// Enfileira a sincronização da rede. Se já houver uma na fila ou rodando, só devolve a situação
    /// (a tela acompanha a que está em andamento). Recusa se a rede sincronizou há menos de 2 minutos.
    /// </summary>
    public async Task<Resultado<SituacaoSincronizacaoDto>> SolicitarAsync(int clienteId, CancellationToken cancelamento)
    {
        List<DateTime?> datas = await EmpresasConectadas(clienteId)
            .Select(e => e.UltimaSincronizacaoEm)
            .ToListAsync(cancelamento);
        if (datas.Count == 0)
        {
            return Resultado<SituacaoSincronizacaoDto>.Invalido(MensagemSemEmpresa);
        }

        SituacaoSincronizacaoDto situacao = Montar(datas);
        if (situacao.EmExecucao)
        {
            return Resultado<SituacaoSincronizacaoDto>.Ok(situacao);
        }

        DateTime agora = _relogio.GetUtcNow().UtcDateTime;
        if (situacao.PodeSincronizarEm.HasValue && situacao.PodeSincronizarEm.Value > agora)
        {
            int segundos = (int)Math.Ceiling((situacao.PodeSincronizarEm.Value - agora).TotalSeconds);
            return Resultado<SituacaoSincronizacaoDto>.Conflito(
                "As contas foram sincronizadas há pouco. Tente de novo em " + segundos + " segundos.");
        }

        _fila.SincronizarRede(clienteId);
        situacao.EmExecucao = true;
        return Resultado<SituacaoSincronizacaoDto>.Ok(situacao);
    }

    private IQueryable<Empresa> EmpresasConectadas(int clienteId)
    {
        return _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == clienteId && e.Ativo && e.BlingRefreshTokenProtegido != null);
    }

    private SituacaoSincronizacaoDto Montar(List<DateTime?> datas)
    {
        var situacao = new SituacaoSincronizacaoDto();
        situacao.EmExecucao = _fila.SincronizacaoEmAndamento();
        foreach (DateTime? data in datas)
        {
            if (!data.HasValue)
            {
                continue;
            }
            if (!situacao.UltimaSincronizacaoEm.HasValue || data.Value > situacao.UltimaSincronizacaoEm.Value)
            {
                situacao.UltimaSincronizacaoEm = data;
            }
        }
        if (situacao.UltimaSincronizacaoEm.HasValue)
        {
            DateTime ultima = DateTime.SpecifyKind(situacao.UltimaSincronizacaoEm.Value, DateTimeKind.Utc);
            situacao.UltimaSincronizacaoEm = ultima;
            situacao.PodeSincronizarEm = ultima.Add(IntervaloMinimo);
        }
        return situacao;
    }
}
