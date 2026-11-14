using Microsoft.EntityFrameworkCore;
using Previsio.Aplicacao.Dtos;
using Previsio.Core.Entidades;

namespace Previsio.Aplicacao.Comum;

/// <summary>
/// Fornecedores das notas já recebidas pela rede (o mais recente de cada CNPJ), para escolher nas regras.
/// </summary>
public static class FornecedoresDaRede
{
    public const int Maximo = 500;

    public static async Task<List<FornecedorDaRedeDto>> ListarAsync(
        IQueryable<NotaFiscal> notas, int clienteId, CancellationToken cancelamento)
    {
        var recentes = await notas
            .Where(n => n.ClienteId == clienteId && n.CnpjFornecedor != "")
            .OrderByDescending(n => n.RecebidaEm)
            .Select(n => new { n.CnpjFornecedor, n.NomeFornecedor })
            .Take(5000)
            .ToListAsync(cancelamento);

        var porCnpj = new Dictionary<string, FornecedorDaRedeDto>();
        foreach (var nota in recentes)
        {
            string cnpj = ApenasDigitos(nota.CnpjFornecedor);
            if (cnpj == "" || porCnpj.ContainsKey(cnpj))
            {
                continue;
            }
            porCnpj[cnpj] = new FornecedorDaRedeDto { Cnpj = cnpj, Nome = nota.NomeFornecedor };
        }
        return porCnpj.Values.OrderBy(f => f.Nome).Take(Maximo).ToList();
    }

    public static string ApenasDigitos(string? texto)
    {
        if (texto == null)
        {
            return "";
        }
        return new string(texto.Where(char.IsDigit).ToArray());
    }
}
