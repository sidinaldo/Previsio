using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;
using Previsio.Core.Abstracoes;
using Previsio.Core.Contratos;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Mapeamentos;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Aplicacao.Clientes;

/// <summary>
/// Cadastro dos clientes (redes) e da caixa de e-mail de onde vêm as notas.
/// </summary>
public class ClientesService
{
    public const string MensagemNaoEncontrado = "Cliente não encontrado.";

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IProtetorSegredos _protetor;

    public ClientesService(IRepositorio<Cliente> clientes, IRepositorio<Empresa> empresas, IProtetorSegredos protetor)
    {
        _clientes = clientes;
        _empresas = empresas;
        _protetor = protetor;
    }

    public async Task<List<ClienteDto>> ListarAsync(CancellationToken cancelamento)
    {
        List<Cliente> clientes = await _clientes.ConsultarSomenteLeitura()
            .OrderBy(c => c.Nome)
            .ToListAsync(cancelamento);
        var erps = await _empresas.ConsultarSomenteLeitura()
            .Select(e => new { e.ClienteId, e.Erp })
            .ToListAsync(cancelamento);
        DateTime agora = DateTime.UtcNow;
        var lista = new List<ClienteDto>();
        foreach (Cliente cliente in clientes)
        {
            IEnumerable<TipoErp> daRede = erps.Where(e => e.ClienteId == cliente.Id).Select(e => e.Erp);
            lista.Add(ClienteMapeamento.ParaDto(cliente, daRede, agora));
        }
        return lista;
    }

    private async Task<ClienteDto> ParaDtoAsync(Cliente cliente, CancellationToken cancelamento)
    {
        List<TipoErp> erps = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == cliente.Id)
            .Select(e => e.Erp)
            .ToListAsync(cancelamento);
        return ClienteMapeamento.ParaDto(cliente, erps);
    }

    public async Task<Resultado<ClienteDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ClienteDto>.NaoEncontrado(MensagemNaoEncontrado);
        }

        return Resultado<ClienteDto>.Ok(await ParaDtoAsync(cliente, cancelamento));
    }

    public async Task<Resultado<ClienteDto>> CriarAsync(NovoClienteDto dados, CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(dados.Nome))
        {
            return Resultado<ClienteDto>.Invalido("Informe o nome do cliente.");
        }

        var cliente = new Cliente();
        cliente.Nome = dados.Nome.Trim();
        cliente.ImapHost = dados.ImapHost.Trim();
        cliente.ImapPorta = dados.ImapPorta;
        cliente.ImapSsl = dados.ImapSsl;
        cliente.ImapUsuario = dados.ImapUsuario.Trim();
        if (!string.IsNullOrEmpty(dados.ImapSenha))
        {
            cliente.ImapSenhaProtegida = _protetor.Proteger(dados.ImapSenha);
        }

        string clientId = (dados.ErpClientId ?? "").Trim();
        string clientSecret = (dados.ErpClientSecret ?? "").Trim();
        if (clientId != "" || clientSecret != "")
        {
            if (clientId == "" || clientSecret == "")
            {
                return Resultado<ClienteDto>.Invalido("Informe o Client ID e o Client Secret do aplicativo do Bling, ou deixe os dois vazios.");
            }
            cliente.ErpClientId = clientId;
            cliente.ErpClientSecretProtegido = _protetor.Proteger(clientSecret);
        }

        if (dados.EmTeste)
        {
            cliente.Contrato = SituacaoContrato.EmTeste;
            cliente.TesteTerminaEm = PeriodoTeste.Termino(RelogioBrasil.Hoje(), PeriodoTeste.DiasPadrao);
        }

        _clientes.Adicionar(cliente);
        await _clientes.SalvarAsync(cancelamento);

        return Resultado<ClienteDto>.Ok(await ParaDtoAsync(cliente, cancelamento));
    }

    /// <summary>
    /// Altera os campos preenchidos. A senha do IMAP só troca se vier preenchida.
    /// </summary>
    public async Task<Resultado<ClienteDto>> AlterarAsync(
        int clienteId, AlteracaoClienteDto dados, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ClienteDto>.NaoEncontrado(MensagemNaoEncontrado);
        }

        if (dados.Nome != null)
        {
            if (string.IsNullOrWhiteSpace(dados.Nome))
            {
                return Resultado<ClienteDto>.Invalido("Informe o nome do cliente.");
            }
            cliente.Nome = dados.Nome.Trim();
        }

        // Guardados para saber se a caixa de e-mail mudou
        string hostAnterior = cliente.ImapHost;
        string usuarioAnterior = cliente.ImapUsuario;

        if (dados.ImapHost != null)
        {
            cliente.ImapHost = dados.ImapHost.Trim();
        }
        if (dados.ImapPorta.HasValue)
        {
            cliente.ImapPorta = dados.ImapPorta.Value;
        }
        if (dados.ImapSsl.HasValue)
        {
            cliente.ImapSsl = dados.ImapSsl.Value;
        }
        if (dados.ImapUsuario != null)
        {
            cliente.ImapUsuario = dados.ImapUsuario.Trim();
        }
        if (!string.IsNullOrEmpty(dados.ImapSenha))
        {
            cliente.ImapSenhaProtegida = _protetor.Proteger(dados.ImapSenha);
        }
        if (dados.Ativo.HasValue)
        {
            cliente.Ativo = dados.Ativo.Value;
        }

        string clientIdAnterior = cliente.ErpClientId ?? "";

        if (dados.ErpClientId != null)
        {
            string clientId = dados.ErpClientId.Trim();
            if (clientId == "")
            {
                // As empresas sem aplicativo próprio passam a usar o global do Previsio
                cliente.ErpClientId = null;
                cliente.ErpClientSecretProtegido = null;
            }
            else
            {
                cliente.ErpClientId = clientId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dados.ErpClientSecret) && !string.IsNullOrEmpty(cliente.ErpClientId))
        {
            cliente.ErpClientSecretProtegido = _protetor.Proteger(dados.ErpClientSecret.Trim());
        }

        if (!string.IsNullOrEmpty(cliente.ErpClientId) && string.IsNullOrEmpty(cliente.ErpClientSecretProtegido))
        {
            return Resultado<ClienteDto>.Invalido("Informe o Client Secret do aplicativo do Bling.");
        }

        // Os tokens pertencem ao aplicativo que autorizou: as empresas que usam o aplicativo
        // da rede precisam conectar de novo
        if (clientIdAnterior != (cliente.ErpClientId ?? ""))
        {
            List<Empresa> semAplicativoProprio = await _empresas.Consultar()
                .Where(e => e.ClienteId == cliente.Id && (e.ErpClientId == null || e.ErpClientId == ""))
                .ToListAsync(cancelamento);
            foreach (Empresa empresa in semAplicativoProprio)
            {
                empresa.ErpAccessTokenProtegido = null;
                empresa.ErpRefreshTokenProtegido = null;
                empresa.ErpTokenExpiraEm = null;
            }
        }

        // Outra caixa de e-mail: os UIDs da caixa anterior não valem nela
        bool trocouCaixa = !string.Equals(hostAnterior, cliente.ImapHost, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(usuarioAnterior, cliente.ImapUsuario, StringComparison.OrdinalIgnoreCase);
        if (trocouCaixa)
        {
            cliente.ImapUidValidity = null;
            cliente.ImapUltimoUid = null;
        }

        await _clientes.SalvarAsync(cancelamento);
        return Resultado<ClienteDto>.Ok(await ParaDtoAsync(cliente, cancelamento));
    }
}
