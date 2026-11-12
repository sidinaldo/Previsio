using Microsoft.EntityFrameworkCore;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Sincronizacao;

/// <summary>
/// Dá nome ao fornecedor das contas a pagar sincronizadas. Primeiro vale o emitente da nota que
/// gerou a conta; nas lançadas direto no Bling, o contato do Bling, buscado uma vez e guardado
/// (renovado depois de alguns dias).
/// </summary>
public class FornecedoresBlingService
{
    // Limite de contatos novos buscados no Bling por empresa a cada sincronização;
    // o resto fica para a próxima, sem atrasar as outras empresas.
    public const int MaximoBuscasPorExecucao = 100;

    private readonly PrevisioDbContext _db;
    private readonly BlingClient _bling;
    private readonly ILogger _logger;

    public FornecedoresBlingService(PrevisioDbContext db, BlingClient bling, ILogger logger)
    {
        _db = db;
        _bling = bling;
        _logger = logger;
    }

    /// <summary>
    /// Preenche FornecedorNome das contas (acompanhadas pelo contexto; quem chama salva).
    /// Devolve quantos contatos foram buscados no Bling.
    /// </summary>
    public async Task<int> PreencherNomesAsync(
        Empresa empresa,
        string conta,
        string token,
        List<ContaPagarSincronizada> contas,
        int diasRenovar,
        DateTime agoraUtc,
        CancellationToken cancelamento)
    {
        if (contas.Count == 0)
        {
            return 0;
        }

        // 1) Contas que vieram de nota lida pelo Previsio: nome do emitente do XML
        List<long> idsContas = contas.Select(c => c.BlingId).ToList();
        var nomesDasNotas = await _db.ParcelasNota.AsNoTracking()
            .Where(p => p.BlingContaPagarId.HasValue
                && idsContas.Contains(p.BlingContaPagarId.Value)
                && p.NotaFiscal!.EmpresaId == empresa.Id)
            .Select(p => new { ContaId = p.BlingContaPagarId!.Value, Nome = p.NotaFiscal!.NomeFornecedor })
            .ToListAsync(cancelamento);

        var nomePorConta = new Dictionary<long, string>();
        foreach (var item in nomesDasNotas)
        {
            if (!string.IsNullOrWhiteSpace(item.Nome))
            {
                nomePorConta[item.ContaId] = item.Nome;
            }
        }

        var semNota = new List<ContaPagarSincronizada>();
        foreach (ContaPagarSincronizada contaPagar in contas)
        {
            string? nome;
            if (nomePorConta.TryGetValue(contaPagar.BlingId, out nome))
            {
                contaPagar.FornecedorNome = Limitar(nome);
            }
            else if (contaPagar.BlingContatoId.HasValue)
            {
                semNota.Add(contaPagar);
            }
        }

        if (semNota.Count == 0)
        {
            return 0;
        }

        // 2) Lançadas direto no Bling: contato guardado ou buscado agora
        List<long> idsContatos = semNota.Select(c => c.BlingContatoId!.Value).Distinct().ToList();
        List<FornecedorBling> guardados = await _db.FornecedoresBling
            .Where(f => f.EmpresaId == empresa.Id && idsContatos.Contains(f.BlingContatoId))
            .ToListAsync(cancelamento);

        var porContato = new Dictionary<long, FornecedorBling>();
        foreach (FornecedorBling guardado in guardados)
        {
            porContato[guardado.BlingContatoId] = guardado;
        }

        DateTime limiteRenovacao = agoraUtc.AddDays(-diasRenovar);
        int buscas = 0;
        foreach (long idContato in idsContatos)
        {
            FornecedorBling? fornecedor;
            porContato.TryGetValue(idContato, out fornecedor);

            bool precisaBuscar = fornecedor == null || fornecedor.AtualizadoEm < limiteRenovacao;
            if (!precisaBuscar || buscas >= MaximoBuscasPorExecucao)
            {
                continue;
            }

            buscas++;
            ContatoBling? contato = await BuscarAsync(empresa, conta, token, idContato, cancelamento);
            if (contato == null || string.IsNullOrWhiteSpace(contato.Nome))
            {
                continue;
            }

            if (fornecedor == null)
            {
                fornecedor = new FornecedorBling();
                fornecedor.EmpresaId = empresa.Id;
                fornecedor.BlingContatoId = idContato;
                _db.FornecedoresBling.Add(fornecedor);
                porContato[idContato] = fornecedor;
            }
            fornecedor.Nome = Limitar(contato.Nome)!;
            fornecedor.Documento = ApenasDigitos(contato.NumeroDocumento);
            fornecedor.AtualizadoEm = agoraUtc;
        }

        foreach (ContaPagarSincronizada contaPagar in semNota)
        {
            FornecedorBling? fornecedor;
            if (porContato.TryGetValue(contaPagar.BlingContatoId!.Value, out fornecedor))
            {
                contaPagar.FornecedorNome = fornecedor.Nome;
            }
        }

        return buscas;
    }

    private async Task<ContatoBling?> BuscarAsync(
        Empresa empresa, string conta, string token, long idContato, CancellationToken cancelamento)
    {
        try
        {
            return await _bling.ObterContatoAsync(conta, token, idContato, cancelamento);
        }
        catch (BlingException erro)
        {
            // Contato excluído ou falha passageira: a conta fica sem nome e tenta de novo na próxima
            _logger.LogWarning(erro, "Não foi possível buscar o contato {Contato} no Bling da empresa {EmpresaId}",
                idContato, empresa.Id);
            return null;
        }
    }

    private static string? Limitar(string? texto)
    {
        if (texto == null)
        {
            return null;
        }
        string limpo = texto.Trim();
        if (limpo.Length > 200)
        {
            return limpo.Substring(0, 200);
        }
        return limpo;
    }

    private static string? ApenasDigitos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }
        string digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length == 0 || digitos.Length > 20)
        {
            return null;
        }
        return digitos;
    }
}
