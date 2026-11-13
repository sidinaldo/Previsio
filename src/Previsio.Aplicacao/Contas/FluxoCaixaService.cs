using Microsoft.EntityFrameworkCore;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Liga ou desliga o fluxo de caixa da rede (contas a receber do Bling no painel).
/// </summary>
public class FluxoCaixaService
{
    private readonly IRepositorio<Cliente> _clientes;

    public FluxoCaixaService(IRepositorio<Cliente> clientes)
    {
        _clientes = clientes;
    }

    public async Task<Resultado<FluxoCaixaDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<FluxoCaixaDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        return Resultado<FluxoCaixaDto>.Ok(ParaDto(cliente));
    }

    public async Task<Resultado<FluxoCaixaDto>> AlterarAsync(int clienteId, FluxoCaixaDto dados, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<FluxoCaixaDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        cliente.MostrarFluxoCaixa = dados.Ativo;
        await _clientes.SalvarAsync(cancelamento);
        return Resultado<FluxoCaixaDto>.Ok(ParaDto(cliente));
    }

    private static FluxoCaixaDto ParaDto(Cliente cliente)
    {
        var dto = new FluxoCaixaDto();
        dto.Ativo = cliente.MostrarFluxoCaixa;
        return dto;
    }
}
