using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Sincronizacao;

/// <summary>
/// Traz as contas a receber do Bling para o fluxo de caixa, na mesma janela de vencimento das
/// contas a pagar. Roda dentro da sincronização da empresa; quem chama salva.
/// </summary>
public class ContasReceberBlingService
{
    private readonly IRepositorio<ContaReceberSincronizada> _contas;
    private readonly BlingClient _bling;
    private readonly FornecedoresBlingService _contatos;
    private readonly ILogger<ContasReceberBlingService> _logger;

    public ContasReceberBlingService(
        IRepositorio<ContaReceberSincronizada> contas,
        BlingClient bling,
        FornecedoresBlingService contatos,
        ILogger<ContasReceberBlingService> logger)
    {
        _contas = contas;
        _bling = bling;
        _contatos = contatos;
        _logger = logger;
    }

    /// <summary>
    /// Atualiza, inclui e remove as contas a receber da janela. Devolve quantas vieram do Bling,
    /// ou null quando o aplicativo ainda não tem o escopo de contas a receber (a empresa precisa
    /// reconectar o Bling): nesse caso as contas a pagar seguem normalmente.
    /// </summary>
    public async Task<int?> SincronizarAsync(
        Empresa empresa,
        string conta,
        string token,
        DateTime vencimentoInicial,
        DateTime vencimentoFinal,
        int diasRenovarContato,
        DateTime agoraUtc,
        CancellationToken cancelamento)
    {
        List<ContaReceberBling> remotas;
        try
        {
            remotas = await _bling.ListarContasReceberAsync(conta, token, vencimentoInicial, vencimentoFinal, cancelamento);
        }
        catch (BlingException erro)
        {
            if (!erro.SemPermissao)
            {
                throw;
            }
            _logger.LogWarning("Empresa {EmpresaId}: o Bling não liberou contas a receber (reconectar com o escopo novo)", empresa.Id);
            return null;
        }

        List<ContaReceberSincronizada> locais = await _contas.Consultar()
            .Where(c => c.EmpresaId == empresa.Id && c.Vencimento >= vencimentoInicial && c.Vencimento <= vencimentoFinal)
            .ToListAsync(cancelamento);

        var locaisPorId = new Dictionary<long, ContaReceberSincronizada>();
        foreach (ContaReceberSincronizada local in locais)
        {
            locaisPorId[local.BlingId] = local;
        }

        var idsRecebidos = new HashSet<long>();
        var semNome = new List<ContaReceberSincronizada>();
        foreach (ContaReceberBling remota in remotas)
        {
            DateTime vencimento;
            if (!DateTime.TryParseExact(remota.Vencimento, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out vencimento))
            {
                continue;
            }
            // O filtro de data da API pode não valer para contas a receber: confere aqui
            if (vencimento.Date < vencimentoInicial || vencimento.Date > vencimentoFinal)
            {
                continue;
            }

            idsRecebidos.Add(remota.Id);

            ContaReceberSincronizada? local;
            if (!locaisPorId.TryGetValue(remota.Id, out local))
            {
                local = new ContaReceberSincronizada();
                local.EmpresaId = empresa.Id;
                local.BlingId = remota.Id;
                _contas.Adicionar(local);
            }

            local.Vencimento = vencimento.Date;
            local.Valor = remota.Valor;
            local.Situacao = remota.Situacao;
            local.SincronizadaEm = agoraUtc;
            if (remota.Contato != null)
            {
                local.BlingContatoId = remota.Contato.Id;
                if (!string.IsNullOrWhiteSpace(remota.Contato.Nome))
                {
                    local.ClienteNome = FornecedoresBlingService.Limitar(remota.Contato.Nome);
                }
                else
                {
                    semNome.Add(local);
                }
            }
        }

        // Contas que sumiram do Bling dentro da janela foram excluídas lá
        foreach (ContaReceberSincronizada local in locais)
        {
            if (!idsRecebidos.Contains(local.BlingId))
            {
                _contas.Remover(local);
            }
        }

        // Quando a lista não traz o nome do cliente: contato guardado ou buscado no Bling
        if (semNome.Count > 0)
        {
            List<long> idsContatos = semNome.Select(c => c.BlingContatoId!.Value).Distinct().ToList();
            NomesDeContatos contatos = await _contatos.NomesDosContatosAsync(
                empresa, conta, token, idsContatos, diasRenovarContato, agoraUtc, cancelamento);
            foreach (ContaReceberSincronizada local in semNome)
            {
                string? nome;
                if (contatos.Nomes.TryGetValue(local.BlingContatoId!.Value, out nome))
                {
                    local.ClienteNome = nome;
                }
            }
        }

        return remotas.Count;
    }
}
