using System.Security.Claims;
using Previsio.Api.Autenticacao;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;
using Previsio.Core.Abstracoes;

namespace Previsio.Api.Servicos;

/// <summary>
/// Verificação de acesso que depende do banco: rotas identificadas por empresa.
/// </summary>
public class AcessoEmpresaService
{
    private readonly IRepositorio<Empresa> _empresas;

    public AcessoEmpresaService(IRepositorio<Empresa> empresas)
    {
        _empresas = empresas;
    }

    /// <summary>
    /// Para o usuário de cliente, confirma que a empresa é do cliente dele.
    /// Administrador sempre pode (a existência da empresa é tratada por quem chama).
    /// </summary>
    public async Task<bool> PodeAcessarAsync(ClaimsPrincipal usuario, int empresaId, CancellationToken cancelamento)
    {
        if (usuario.EhAdmin())
        {
            return true;
        }

        int? clienteId = usuario.ClienteId();
        if (!clienteId.HasValue)
        {
            return false;
        }

        return await _empresas.ExisteAsync(e => e.Id == empresaId && e.ClienteId == clienteId.Value, cancelamento);
    }
}
