using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;
using Previsio.Core.Abstracoes;
using Previsio.Core.Contratos;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Servicos;

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
        DateTime agora = DateTime.UtcNow;
        return clientes.Select(c => ClienteMapeamento.ParaDto(c, agora)).ToList();
    }

    public async Task<Resultado<ClienteDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ClienteDto>.NaoEncontrado(MensagemNaoEncontrado);
        }

        return Resultado<ClienteDto>.Ok(ClienteMapeamento.ParaDto(cliente));
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

        string clientId = (dados.BlingClientId ?? "").Trim();
        string clientSecret = (dados.BlingClientSecret ?? "").Trim();
        if (clientId != "" || clientSecret != "")
        {
            if (clientId == "" || clientSecret == "")
            {
                return Resultado<ClienteDto>.Invalido("Informe o Client ID e o Client Secret do aplicativo do Bling, ou deixe os dois vazios.");
            }
            cliente.BlingClientId = clientId;
            cliente.BlingClientSecretProtegido = _protetor.Proteger(clientSecret);
        }

        if (dados.EmTeste)
        {
            cliente.Contrato = SituacaoContrato.EmTeste;
            cliente.TesteTerminaEm = PeriodoTeste.Termino(RelogioBrasil.Hoje(), PeriodoTeste.DiasPadrao);
        }

        _clientes.Adicionar(cliente);
        await _clientes.SalvarAsync(cancelamento);

        return Resultado<ClienteDto>.Ok(ClienteMapeamento.ParaDto(cliente));
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

        string clientIdAnterior = cliente.BlingClientId ?? "";

        if (dados.BlingClientId != null)
        {
            string clientId = dados.BlingClientId.Trim();
            if (clientId == "")
            {
                // As empresas sem aplicativo próprio passam a usar o global do Previsio
                cliente.BlingClientId = null;
                cliente.BlingClientSecretProtegido = null;
            }
            else
            {
                cliente.BlingClientId = clientId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dados.BlingClientSecret) && !string.IsNullOrEmpty(cliente.BlingClientId))
        {
            cliente.BlingClientSecretProtegido = _protetor.Proteger(dados.BlingClientSecret.Trim());
        }

        if (!string.IsNullOrEmpty(cliente.BlingClientId) && string.IsNullOrEmpty(cliente.BlingClientSecretProtegido))
        {
            return Resultado<ClienteDto>.Invalido("Informe o Client Secret do aplicativo do Bling.");
        }

        // Os tokens pertencem ao aplicativo que autorizou: as empresas que usam o aplicativo
        // da rede precisam conectar de novo
        if (clientIdAnterior != (cliente.BlingClientId ?? ""))
        {
            List<Empresa> semAplicativoProprio = await _empresas.Consultar()
                .Where(e => e.ClienteId == cliente.Id && (e.BlingClientId == null || e.BlingClientId == ""))
                .ToListAsync(cancelamento);
            foreach (Empresa empresa in semAplicativoProprio)
            {
                empresa.BlingAccessTokenProtegido = null;
                empresa.BlingRefreshTokenProtegido = null;
                empresa.BlingTokenExpiraEm = null;
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
        return Resultado<ClienteDto>.Ok(ClienteMapeamento.ParaDto(cliente));
    }
}
