using Microsoft.EntityFrameworkCore;
using Previsio.Api.Dtos;
using Previsio.Api.Mapeamentos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;

namespace Previsio.Api.Servicos;

/// <summary>
/// Histórico das falhas ao ler notas do e-mail e ao lançá-las no Bling.
/// </summary>
public class FalhasService
{
    public const int TamanhoPagina = 50;

    private readonly IRepositorio<FalhaProcessamento> _falhas;

    public FalhasService(IRepositorio<FalhaProcessamento> falhas)
    {
        _falhas = falhas;
    }

    /// <summary>
    /// Falhas do cliente; por padrão, da mais recente para a mais antiga.
    /// Filtros opcionais: empresa, nota, etapa, período (dias no horário de Brasília) e busca por NF,
    /// fornecedor, empresa ou CNPJ. Ordem: data, etapa, empresa, nota, fornecedor ou valor ("-" = decrescente).
    /// </summary>
    public async Task<Resultado<PaginaFalhasDto>> ListarAsync(
        int clienteId,
        int? empresaId,
        int? notaId,
        string? etapa,
        DateTime? de,
        DateTime? ate,
        string? busca,
        string? ordem,
        int pagina,
        CancellationToken cancelamento)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        IQueryable<FalhaProcessamento> consulta = _falhas.ConsultarSomenteLeitura().Where(f => f.ClienteId == clienteId);

        if (empresaId.HasValue)
        {
            consulta = consulta.Where(f => f.EmpresaId == empresaId.Value);
        }

        if (notaId.HasValue)
        {
            consulta = consulta.Where(f => f.NotaFiscalId == notaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(etapa))
        {
            EtapaFalha etapaEscolhida;
            if (!Enum.TryParse(etapa, true, out etapaEscolhida) || !Enum.IsDefined(typeof(EtapaFalha), etapaEscolhida))
            {
                return Resultado<PaginaFalhasDto>.Invalido("Etapa inválida.");
            }
            consulta = consulta.Where(f => f.Etapa == etapaEscolhida);
        }

        string? erroPeriodo = ValidacaoPeriodo.Validar(de, ate);
        if (erroPeriodo != null)
        {
            return Resultado<PaginaFalhasDto>.Invalido(erroPeriodo);
        }

        // As datas chegam como dia no horário de Brasília; no banco tudo é UTC
        if (de.HasValue)
        {
            DateTime inicioUtc = RelogioBrasil.InicioDoDiaEmUtc(de.Value);
            consulta = consulta.Where(f => f.OcorridaEm >= inicioUtc);
        }

        if (ate.HasValue)
        {
            DateTime fimUtc = RelogioBrasil.InicioDoDiaEmUtc(ate.Value.AddDays(1));
            consulta = consulta.Where(f => f.OcorridaEm < fimUtc);
        }

        BuscaTexto? termo = BuscaTexto.Ler(busca);
        if (termo != null)
        {
            string texto = termo.Texto;
            string digitos = termo.Digitos;
            if (termo.TemDigitos)
            {
                consulta = consulta.Where(f =>
                    (f.NumeroNota != null && f.NumeroNota.Contains(texto))
                    || (f.Fornecedor != null && f.Fornecedor.ToLower().Contains(texto))
                    || (f.Empresa != null && f.Empresa.Nome.ToLower().Contains(texto))
                    || (f.CnpjFornecedor != null && f.CnpjFornecedor.Contains(digitos))
                    || (f.ChaveAcesso != null && f.ChaveAcesso.Contains(digitos)));
            }
            else
            {
                consulta = consulta.Where(f =>
                    (f.NumeroNota != null && f.NumeroNota.Contains(texto))
                    || (f.Fornecedor != null && f.Fornecedor.ToLower().Contains(texto))
                    || (f.Empresa != null && f.Empresa.Nome.ToLower().Contains(texto)));
            }
        }

        IOrderedQueryable<FalhaProcessamento>? ordenada = Ordenar(consulta, OrdemLista.Ler(ordem, "data", true));
        if (ordenada == null)
        {
            return Resultado<PaginaFalhasDto>.Invalido(MensagemOrdemInvalida);
        }

        int total = await consulta.CountAsync(cancelamento);

        List<FalhaProcessamento> falhas = await ordenada
            .ThenByDescending(f => f.Id)
            .Include(f => f.Empresa)
            .Skip((pagina - 1) * TamanhoPagina)
            .Take(TamanhoPagina)
            .ToListAsync(cancelamento);

        var resposta = new PaginaFalhasDto();
        resposta.Total = total;
        resposta.Pagina = pagina;
        resposta.TamanhoPagina = TamanhoPagina;
        foreach (FalhaProcessamento falha in falhas)
        {
            resposta.Itens.Add(FalhaMapeamento.ParaDto(falha));
        }
        return Resultado<PaginaFalhasDto>.Ok(resposta);
    }

    public const string MensagemOrdemInvalida =
        "Ordenação inválida. Use data, etapa, empresa, nota, fornecedor ou valor.";

    /// <summary>
    /// Ordena pela coluna pedida; nula quando a coluna não existe.
    /// </summary>
    private static IOrderedQueryable<FalhaProcessamento>? Ordenar(IQueryable<FalhaProcessamento> consulta, OrdemLista ordem)
    {
        bool desc = ordem.Decrescente;
        if (ordem.Coluna == "data")
        {
            return desc ? consulta.OrderByDescending(f => f.OcorridaEm) : consulta.OrderBy(f => f.OcorridaEm);
        }
        if (ordem.Coluna == "etapa")
        {
            return desc ? consulta.OrderByDescending(f => f.Etapa) : consulta.OrderBy(f => f.Etapa);
        }
        if (ordem.Coluna == "empresa")
        {
            if (desc)
            {
                return consulta.OrderByDescending(f => f.Empresa != null ? f.Empresa.Nome : "");
            }
            return consulta.OrderBy(f => f.Empresa != null ? f.Empresa.Nome : "");
        }
        if (ordem.Coluna == "nota")
        {
            if (desc)
            {
                return consulta.OrderByDescending(f => f.NumeroNota != null ? f.NumeroNota.Length : 0)
                    .ThenByDescending(f => f.NumeroNota);
            }
            return consulta.OrderBy(f => f.NumeroNota != null ? f.NumeroNota.Length : 0).ThenBy(f => f.NumeroNota);
        }
        if (ordem.Coluna == "fornecedor")
        {
            return desc ? consulta.OrderByDescending(f => f.Fornecedor) : consulta.OrderBy(f => f.Fornecedor);
        }
        if (ordem.Coluna == "valor")
        {
            return desc ? consulta.OrderByDescending(f => f.ValorNota) : consulta.OrderBy(f => f.ValorNota);
        }
        return null;
    }
}
