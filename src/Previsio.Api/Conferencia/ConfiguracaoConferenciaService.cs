using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Conferencia;

/// <summary>
/// Configuração da conferência com o Bling de cada rede.
/// </summary>
public class ConfiguracaoConferenciaService
{
    public const decimal MaximoToleranciaValor = 5m;
    public const int MaximoToleranciaDias = 5;

    private readonly IRepositorio<Cliente> _clientes;

    public ConfiguracaoConferenciaService(IRepositorio<Cliente> clientes)
    {
        _clientes = clientes;
    }

    public async Task<Resultado<ConferenciaBlingDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ConferenciaBlingDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        return Resultado<ConferenciaBlingDto>.Ok(ParaDto(cliente));
    }

    public async Task<Resultado<ConferenciaBlingDto>> AlterarAsync(int clienteId, ConferenciaBlingDto dados, CancellationToken cancelamento)
    {
        if (dados.ToleranciaValor < 0 || dados.ToleranciaValor > MaximoToleranciaValor)
        {
            return Resultado<ConferenciaBlingDto>.Invalido("A diferença de valor vai de R$ 0,00 a R$ 5,00.");
        }
        if (dados.ToleranciaDias < 0 || dados.ToleranciaDias > MaximoToleranciaDias)
        {
            return Resultado<ConferenciaBlingDto>.Invalido("A diferença de vencimento vai de 0 a 5 dias.");
        }
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ConferenciaBlingDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        cliente.ConferirBlingAntesDeLancar = dados.ConferirAntesDeLancar;
        cliente.ConferenciaExigeNumeroNota = dados.ExigeNumeroNota;
        cliente.ConferenciaToleranciaValor = Math.Round(dados.ToleranciaValor, 2);
        cliente.ConferenciaToleranciaDias = dados.ToleranciaDias;
        cliente.ConferenciaFecharPendentes = dados.FecharPendentes;
        await _clientes.SalvarAsync(cancelamento);
        return Resultado<ConferenciaBlingDto>.Ok(ParaDto(cliente));
    }

    private static ConferenciaBlingDto ParaDto(Cliente cliente)
    {
        var dto = new ConferenciaBlingDto();
        dto.ConferirAntesDeLancar = cliente.ConferirBlingAntesDeLancar;
        dto.ExigeNumeroNota = cliente.ConferenciaExigeNumeroNota;
        dto.ToleranciaValor = cliente.ConferenciaToleranciaValor;
        dto.ToleranciaDias = cliente.ConferenciaToleranciaDias;
        dto.FecharPendentes = cliente.ConferenciaFecharPendentes;
        return dto;
    }
}
