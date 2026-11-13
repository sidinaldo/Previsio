using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Categorias;

/// <summary>
/// Escolhe a categoria do Bling para as contas de uma nota, nesta ordem:
/// 1) regra da rede para o fornecedor (pelo nome da categoria);
/// 2) a categoria da última conta desse fornecedor no Bling da empresa (aprende com quem corrigiu lá);
/// 3) a categoria padrão da rede ou, sem ela, o padrão automático (Categorias:PadraoAutomatico).
/// A regra e a padrão são nomes: vale a categoria de mesmo nome no Bling da empresa. Se a empresa
/// não tiver, a conta vai sem categoria (a tela de categorias mostra o aviso).
/// </summary>
public class CategoriaLancamentoService
{
    private readonly IRepositorio<RegraCategoria> _regras;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<ContaPagarSincronizada> _contas;
    private readonly IRepositorio<CategoriaFinanceira> _categorias;
    private readonly CategoriasBlingService _categoriasBling;
    private readonly CategoriasOptions _opcoes;

    public CategoriaLancamentoService(
        IRepositorio<RegraCategoria> regras,
        IRepositorio<Cliente> clientes,
        IRepositorio<ContaPagarSincronizada> contas,
        IRepositorio<CategoriaFinanceira> categorias,
        CategoriasBlingService categoriasBling,
        IOptions<CategoriasOptions> opcoes)
    {
        _opcoes = opcoes.Value;
        _regras = regras;
        _clientes = clientes;
        _contas = contas;
        _categorias = categorias;
        _categoriasBling = categoriasBling;
    }

    /// <summary>Id da categoria no Bling da empresa, ou null para lançar sem categoria.</summary>
    public async Task<long?> EscolherAsync(
        NotaFiscal nota, Empresa empresa, long contatoId, string conta, string token, CancellationToken cancelamento)
    {
        // Garante a lista de categorias da empresa (busca no Bling se ainda não tem)
        await _categoriasBling.AtualizarSePrecisoAsync(empresa, conta, token, DateTime.UtcNow, cancelamento);
        await _categorias.SalvarAsync(cancelamento);

        // 1) Regra da rede para o fornecedor
        string cnpj = RegrasCategoria.ApenasDigitos(nota.CnpjFornecedor);
        int clienteId = nota.ClienteId;
        string? regra = await _regras.ConsultarSomenteLeitura()
            .Where(r => r.ClienteId == clienteId && r.CnpjFornecedor == cnpj)
            .Select(r => r.Categoria)
            .FirstOrDefaultAsync(cancelamento);
        if (regra != null)
        {
            return await PorNomeAsync(empresa.Id, regra, cancelamento);
        }

        // 2) Última conta desse fornecedor no Bling da empresa com categoria
        int empresaId = empresa.Id;
        long? aprendida = await _contas.ConsultarSomenteLeitura()
            .Where(c => c.EmpresaId == empresaId && c.BlingContatoId == contatoId && c.CategoriaBlingId != null)
            .OrderByDescending(c => c.CategoriaConferidaEm)
            .ThenByDescending(c => c.Vencimento)
            .Select(c => c.CategoriaBlingId)
            .FirstOrDefaultAsync(cancelamento);
        if (aprendida.HasValue)
        {
            return aprendida;
        }

        // 3) Categoria padrão da rede (sem ela, a automática)
        string? padrao = await _clientes.ConsultarSomenteLeitura()
            .Where(c => c.Id == clienteId)
            .Select(c => c.CategoriaPadrao)
            .FirstOrDefaultAsync(cancelamento);
        if (string.IsNullOrWhiteSpace(padrao))
        {
            padrao = _opcoes.PadraoAutomaticoValido();
        }
        if (padrao == null)
        {
            return null;
        }
        return await PorNomeAsync(empresa.Id, padrao, cancelamento);
    }

    private async Task<long?> PorNomeAsync(int empresaId, string nome, CancellationToken cancelamento)
    {
        string procurado = RegrasCategoria.Normalizar(nome);
        List<CategoriaFinanceira> categorias = await _categorias.ConsultarSomenteLeitura()
            .Where(c => c.EmpresaId == empresaId && c.Tipo != RegrasCategoria.TipoReceita)
            .ToListAsync(cancelamento);
        foreach (CategoriaFinanceira categoria in categorias)
        {
            if (RegrasCategoria.Normalizar(categoria.Descricao) == procurado)
            {
                return categoria.BlingId;
            }
        }
        return null;
    }
}
