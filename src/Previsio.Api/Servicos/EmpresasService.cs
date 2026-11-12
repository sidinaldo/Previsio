using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data.Repositorios;
using Previsio.Core.Abstracoes;

namespace Previsio.Api.Servicos;

/// <summary>
/// Empresas (CNPJs) de um cliente e o aplicativo do Bling de cada uma.
/// </summary>
public class EmpresasService
{
    public const string MensagemNaoEncontrada = "Empresa não encontrada.";

    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly AcessoEmpresaService _acesso;
    private readonly ProtetorSegredos _protetor;

    public EmpresasService(
        IRepositorio<Empresa> empresas,
        IRepositorio<Cliente> clientes,
        AcessoEmpresaService acesso,
        ProtetorSegredos protetor)
    {
        _empresas = empresas;
        _clientes = clientes;
        _acesso = acesso;
        _protetor = protetor;
    }

    public async Task<List<EmpresaDto>> ListarDoClienteAsync(int clienteId, CancellationToken cancelamento)
    {
        List<Empresa> empresas = await _empresas.ConsultarSomenteLeitura()
            .Include(e => e.Cliente)
            .Where(e => e.ClienteId == clienteId)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);

        var lista = new List<EmpresaDto>();
        foreach (Empresa empresa in empresas)
        {
            lista.Add(EmpresaMapeamento.ParaDto(empresa));
        }
        return lista;
    }

    public async Task<Resultado<EmpresaDto>> CriarAsync(
        int clienteId, NovaEmpresaDto dados, CancellationToken cancelamento)
    {
        Cliente? rede = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (rede == null)
        {
            return Resultado<EmpresaDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        string cnpj = LeitorNFe.SomenteDigitos(dados.Cnpj);
        if (cnpj.Length != 14)
        {
            return Resultado<EmpresaDto>.Invalido("CNPJ inválido.");
        }

        bool cnpjRepetido = await _empresas.ExisteAsync(e => e.ClienteId == clienteId && e.Cnpj == cnpj, cancelamento);
        if (cnpjRepetido)
        {
            return Resultado<EmpresaDto>.Conflito("Já existe uma empresa com esse CNPJ para este cliente.");
        }

        var empresa = new Empresa();
        empresa.ClienteId = clienteId;
        empresa.Cliente = rede;
        empresa.Nome = dados.Nome.Trim();
        empresa.Cnpj = cnpj;

        string clientId = (dados.BlingClientId ?? "").Trim();
        string clientSecret = (dados.BlingClientSecret ?? "").Trim();
        if (clientId != "" || clientSecret != "")
        {
            if (clientId == "" || clientSecret == "")
            {
                return Resultado<EmpresaDto>.Invalido("Informe o Client ID e o Client Secret do aplicativo do Bling, ou deixe os dois vazios.");
            }
            empresa.BlingClientId = clientId;
            empresa.BlingClientSecretProtegido = _protetor.Proteger(clientSecret);
        }

        string host = (dados.ImapHost ?? "").Trim();
        if (host != "")
        {
            empresa.ImapHost = host;
            empresa.ImapUsuario = (dados.ImapUsuario ?? "").Trim();
            if (dados.ImapPorta.HasValue)
            {
                empresa.ImapPorta = dados.ImapPorta.Value;
            }
            if (dados.ImapSsl.HasValue)
            {
                empresa.ImapSsl = dados.ImapSsl.Value;
            }
            if (!string.IsNullOrEmpty(dados.ImapSenha))
            {
                empresa.ImapSenhaProtegida = _protetor.Proteger(dados.ImapSenha);
            }
        }

        _empresas.Adicionar(empresa);
        await _empresas.SalvarAsync(cancelamento);

        return Resultado<EmpresaDto>.Ok(EmpresaMapeamento.ParaDto(empresa));
    }

    /// <summary>
    /// Altera o nome, ativa/desativa a empresa e cadastra o aplicativo privado do Bling dela.
    /// O CNPJ não pode ser alterado.
    /// </summary>
    public async Task<Resultado<EmpresaDto>> AlterarAsync(
        int empresaId, AlteracaoEmpresaDto dados, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        Empresa? empresa = await _empresas.Consultar()
            .Include(e => e.Cliente)
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancelamento);
        if (empresa == null)
        {
            return Resultado<EmpresaDto>.NaoEncontrado(MensagemNaoEncontrada);
        }

        // Empresa de outro cliente responde igual a inexistente, para não revelar ids
        bool permitido = await _acesso.PodeAcessarAsync(usuario, empresaId, cancelamento);
        if (!permitido)
        {
            return Resultado<EmpresaDto>.NaoEncontrado(MensagemNaoEncontrada);
        }

        if (dados.Nome != null)
        {
            if (string.IsNullOrWhiteSpace(dados.Nome))
            {
                return Resultado<EmpresaDto>.Invalido("Informe o nome da empresa.");
            }
            empresa.Nome = dados.Nome.Trim();
        }

        if (dados.Ativo.HasValue)
        {
            empresa.Ativo = dados.Ativo.Value;
        }

        string clientIdAnterior = empresa.BlingClientId ?? "";

        if (dados.BlingClientId != null)
        {
            string clientId = dados.BlingClientId.Trim();
            if (clientId == "")
            {
                // Volta a usar o aplicativo global do Previsio
                empresa.BlingClientId = null;
                empresa.BlingClientSecretProtegido = null;
            }
            else
            {
                empresa.BlingClientId = clientId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dados.BlingClientSecret) && !string.IsNullOrEmpty(empresa.BlingClientId))
        {
            empresa.BlingClientSecretProtegido = _protetor.Proteger(dados.BlingClientSecret.Trim());
        }

        if (!string.IsNullOrEmpty(empresa.BlingClientId) && string.IsNullOrEmpty(empresa.BlingClientSecretProtegido))
        {
            return Resultado<EmpresaDto>.Invalido("Informe o Client Secret do aplicativo do Bling.");
        }

        AlterarEmailProprio(empresa, dados);

        // Os tokens pertencem ao aplicativo que autorizou: outro aplicativo exige conectar de novo
        if (clientIdAnterior != (empresa.BlingClientId ?? ""))
        {
            empresa.BlingAccessTokenProtegido = null;
            empresa.BlingRefreshTokenProtegido = null;
            empresa.BlingTokenExpiraEm = null;
        }

        await _empresas.SalvarAsync(cancelamento);
        return Resultado<EmpresaDto>.Ok(EmpresaMapeamento.ParaDto(empresa));
    }

    /// <summary>
    /// Caixa de e-mail própria da empresa: mesmas regras da caixa da rede.
    /// Servidor vazio remove a caixa própria.
    /// </summary>
    private void AlterarEmailProprio(Empresa empresa, AlteracaoEmpresaDto dados)
    {
        // Guardados para saber se a caixa de e-mail mudou
        string hostAnterior = empresa.ImapHost;
        string usuarioAnterior = empresa.ImapUsuario;

        if (dados.ImapHost != null)
        {
            string host = dados.ImapHost.Trim();
            if (host == "")
            {
                empresa.ImapHost = "";
                empresa.ImapUsuario = "";
                empresa.ImapSenhaProtegida = "";
            }
            else
            {
                empresa.ImapHost = host;
            }
        }

        if (empresa.ImapHost != "")
        {
            if (dados.ImapPorta.HasValue)
            {
                empresa.ImapPorta = dados.ImapPorta.Value;
            }
            if (dados.ImapSsl.HasValue)
            {
                empresa.ImapSsl = dados.ImapSsl.Value;
            }
            if (dados.ImapUsuario != null)
            {
                empresa.ImapUsuario = dados.ImapUsuario.Trim();
            }
            if (!string.IsNullOrEmpty(dados.ImapSenha))
            {
                empresa.ImapSenhaProtegida = _protetor.Proteger(dados.ImapSenha);
            }
        }

        // Outra caixa de e-mail: os UIDs da caixa anterior não valem nela
        bool trocouCaixa = !string.Equals(hostAnterior, empresa.ImapHost, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(usuarioAnterior, empresa.ImapUsuario, StringComparison.OrdinalIgnoreCase);
        if (trocouCaixa)
        {
            empresa.ImapUidValidity = null;
            empresa.ImapUltimoUid = null;
        }
    }
}
