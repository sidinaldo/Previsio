using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Api.Servicos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Contratos;

/// <summary>
/// Teste grátis e contratação das redes (feito pelo admin; a cobrança fica para depois).
/// </summary>
public class ContratosService
{
    public const int MaximoDiasProrrogacao = 60;

    private readonly IRepositorio<Cliente> _clientes;
    private readonly TimeProvider _relogio;

    public ContratosService(IRepositorio<Cliente> clientes, TimeProvider relogio)
    {
        _clientes = clientes;
        _relogio = relogio;
    }

    /// <summary>
    /// A rede contratou: volta a ser processada normalmente, sem prazo.
    /// </summary>
    public async Task<Resultado<ClienteDto>> ContratarAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ClienteDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        cliente.Contrato = SituacaoContrato.Contratado;
        cliente.ContratadoEm = Agora();
        await _clientes.SalvarAsync(cancelamento);
        return Resultado<ClienteDto>.Ok(ClienteMapeamento.ParaDto(cliente, Agora()));
    }

    /// <summary>
    /// Dá mais N dias de teste: contados do fim atual, ou de hoje se o teste já acabou.
    /// Também coloca em teste uma rede contratada (ex.: voltou atrás antes de pagar).
    /// </summary>
    public async Task<Resultado<ClienteDto>> ProrrogarAsync(int clienteId, int dias, CancellationToken cancelamento)
    {
        if (dias < 1 || dias > MaximoDiasProrrogacao)
        {
            return Resultado<ClienteDto>.Invalido("Informe de 1 a " + MaximoDiasProrrogacao + " dias.");
        }
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ClienteDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        DateTime agora = Agora();
        DateTime hoje = PeriodoTeste.HojeBrasil(agora);
        DateTime inicio = hoje;
        if (cliente.Contrato == SituacaoContrato.EmTeste && cliente.TesteTerminaEm.HasValue && cliente.TesteTerminaEm.Value > agora)
        {
            inicio = PeriodoTeste.UltimoDia(cliente.TesteTerminaEm.Value).AddDays(1);
        }
        cliente.Contrato = SituacaoContrato.EmTeste;
        cliente.TesteTerminaEm = PeriodoTeste.Termino(inicio, dias);
        // Os avisos de fim valem para o novo prazo
        cliente.TesteAvisoFinalEnviadoEm = null;
        cliente.TesteAvisoEncerradoEnviadoEm = null;
        await _clientes.SalvarAsync(cancelamento);
        return Resultado<ClienteDto>.Ok(ClienteMapeamento.ParaDto(cliente, agora));
    }

    private DateTime Agora()
    {
        return _relogio.GetUtcNow().UtcDateTime;
    }
}
