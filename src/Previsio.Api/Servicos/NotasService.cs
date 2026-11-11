using System.Security.Claims;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Autenticacao;
using Previsio.Api.Dtos;
using Previsio.Api.Jobs;
using Previsio.Api.Mapeamentos;
using Previsio.Core.Entidades;
using Previsio.Data.Repositorios;

namespace Previsio.Api.Servicos;

/// <summary>
/// Notas lidas do e-mail: listagens e reprocessamento do lançamento no Bling.
/// </summary>
public class NotasService
{
    public const int TamanhoPagina = 50;

    // Máximo de linhas da tela de notas pendentes
    public const int MaximoPendentes = 500;

    // Filtro de situação que junta todas as que ainda não viraram conta a pagar
    public const string SituacaoPendentes = "Pendentes";

    public const int TamanhoMaximoObservacao = 500;

    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IBackgroundJobClient _jobs;

    public NotasService(IRepositorio<NotaFiscal> notas, IBackgroundJobClient jobs)
    {
        _notas = notas;
        _jobs = jobs;
    }

    /// <summary>
    /// Notas que chegaram no e-mail e ainda não viraram conta a pagar no Bling.
    /// </summary>
    public async Task<List<NotaDto>> ListarPendentesAsync(int clienteId, int? empresaId, CancellationToken cancelamento)
    {
        IQueryable<NotaFiscal> consulta = _notas.ConsultarSomenteLeitura()
            .Include(n => n.Empresa)
            .Include(n => n.EmpresaCaixa)
            .Include(n => n.Parcelas)
            .Where(n => n.ClienteId == clienteId && StatusPendentes.Todos.Contains(n.Status));

        if (empresaId.HasValue)
        {
            consulta = consulta.Where(n => n.EmpresaId == empresaId.Value);
        }

        List<NotaFiscal> notas = await consulta
            .OrderBy(n => n.RecebidaEm)
            .Take(MaximoPendentes)
            .ToListAsync(cancelamento);

        var lista = new List<NotaDto>();
        foreach (NotaFiscal nota in notas)
        {
            lista.Add(NotaMapeamento.ParaNotaDto(nota));
        }
        return lista;
    }

    /// <summary>
    /// Todas as notas lidas do e-mail (lançadas ou não), com as parcelas; por padrão, da recebida mais
    /// recente para a mais antiga. Filtros opcionais: empresa, situação (nome da situação ou "Pendentes"),
    /// período em que a nota chegou (dias no horário de Brasília) e busca por NF, fornecedor, empresa ou CNPJ.
    /// Ordem: recebida, numero, fornecedor, empresa, emissao, valor, vencimento ou situacao ("-" = decrescente).
    /// </summary>
    public async Task<Resultado<PaginaNotasDto>> ListarAsync(
        int clienteId,
        int? empresaId,
        string? situacao,
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

        IQueryable<NotaFiscal> consulta = _notas.ConsultarSomenteLeitura().Where(n => n.ClienteId == clienteId);

        if (empresaId.HasValue)
        {
            consulta = consulta.Where(n => n.EmpresaId == empresaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(situacao))
        {
            if (string.Equals(situacao, SituacaoPendentes, StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta.Where(n => StatusPendentes.Todos.Contains(n.Status));
            }
            else
            {
                StatusNota escolhida;
                if (!Enum.TryParse(situacao, true, out escolhida) || !Enum.IsDefined(typeof(StatusNota), escolhida))
                {
                    return Resultado<PaginaNotasDto>.Invalido("Situação inválida.");
                }
                consulta = consulta.Where(n => n.Status == escolhida);
            }
        }

        string? erroPeriodo = ValidacaoPeriodo.Validar(de, ate);
        if (erroPeriodo != null)
        {
            return Resultado<PaginaNotasDto>.Invalido(erroPeriodo);
        }

        if (de.HasValue)
        {
            DateTime inicioUtc = RelogioBrasil.InicioDoDiaEmUtc(de.Value);
            consulta = consulta.Where(n => n.RecebidaEm >= inicioUtc);
        }

        if (ate.HasValue)
        {
            DateTime fimUtc = RelogioBrasil.InicioDoDiaEmUtc(ate.Value.AddDays(1));
            consulta = consulta.Where(n => n.RecebidaEm < fimUtc);
        }

        BuscaTexto? termo = BuscaTexto.Ler(busca);
        if (termo != null)
        {
            string texto = termo.Texto;
            string digitos = termo.Digitos;
            if (termo.TemDigitos)
            {
                consulta = consulta.Where(n =>
                    n.Numero.Contains(texto)
                    || n.NomeFornecedor.ToLower().Contains(texto)
                    || (n.Empresa != null && n.Empresa.Nome.ToLower().Contains(texto))
                    || n.CnpjFornecedor.Contains(digitos)
                    || n.CnpjDestinatario.Contains(digitos)
                    || n.ChaveAcesso.Contains(digitos));
            }
            else
            {
                consulta = consulta.Where(n =>
                    n.Numero.Contains(texto)
                    || n.NomeFornecedor.ToLower().Contains(texto)
                    || (n.Empresa != null && n.Empresa.Nome.ToLower().Contains(texto)));
            }
        }

        IOrderedQueryable<NotaFiscal>? ordenada = Ordenar(consulta, OrdemLista.Ler(ordem, "recebida", true));
        if (ordenada == null)
        {
            return Resultado<PaginaNotasDto>.Invalido(MensagemOrdemInvalida);
        }

        int total = await consulta.CountAsync(cancelamento);

        List<NotaFiscal> notas = await ordenada
            .ThenByDescending(n => n.Id)
            .Include(n => n.Empresa)
            .Include(n => n.EmpresaCaixa)
            .Include(n => n.Parcelas)
            .Skip((pagina - 1) * TamanhoPagina)
            .Take(TamanhoPagina)
            .ToListAsync(cancelamento);

        var resposta = new PaginaNotasDto();
        resposta.Total = total;
        resposta.Pagina = pagina;
        resposta.TamanhoPagina = TamanhoPagina;
        foreach (NotaFiscal nota in notas)
        {
            resposta.Itens.Add(NotaMapeamento.ParaNotaRecebidaDto(nota));
        }
        return Resultado<PaginaNotasDto>.Ok(resposta);
    }

    public const string MensagemOrdemInvalida =
        "Ordenação inválida. Use recebida, numero, fornecedor, empresa, emissao, valor, vencimento ou situacao.";

    /// <summary>
    /// Ordena pela coluna pedida; nula quando a coluna não existe.
    /// O número da nota é texto: ordena pelo tamanho antes, para 900 vir antes de 48213.
    /// </summary>
    private static IOrderedQueryable<NotaFiscal>? Ordenar(IQueryable<NotaFiscal> consulta, OrdemLista ordem)
    {
        bool desc = ordem.Decrescente;
        if (ordem.Coluna == "recebida")
        {
            return desc ? consulta.OrderByDescending(n => n.RecebidaEm) : consulta.OrderBy(n => n.RecebidaEm);
        }
        if (ordem.Coluna == "numero")
        {
            if (desc)
            {
                return consulta.OrderByDescending(n => n.Numero.Length).ThenByDescending(n => n.Numero);
            }
            return consulta.OrderBy(n => n.Numero.Length).ThenBy(n => n.Numero);
        }
        if (ordem.Coluna == "fornecedor")
        {
            return desc ? consulta.OrderByDescending(n => n.NomeFornecedor) : consulta.OrderBy(n => n.NomeFornecedor);
        }
        if (ordem.Coluna == "empresa")
        {
            if (desc)
            {
                return consulta.OrderByDescending(n => n.Empresa != null ? n.Empresa.Nome : "");
            }
            return consulta.OrderBy(n => n.Empresa != null ? n.Empresa.Nome : "");
        }
        if (ordem.Coluna == "emissao")
        {
            return desc ? consulta.OrderByDescending(n => n.DataEmissao) : consulta.OrderBy(n => n.DataEmissao);
        }
        if (ordem.Coluna == "valor")
        {
            return desc ? consulta.OrderByDescending(n => n.ValorTotal) : consulta.OrderBy(n => n.ValorTotal);
        }
        if (ordem.Coluna == "vencimento")
        {
            if (desc)
            {
                return consulta.OrderByDescending(n => n.Parcelas.Min(p => (DateTime?)p.Vencimento));
            }
            return consulta.OrderBy(n => n.Parcelas.Min(p => (DateTime?)p.Vencimento));
        }
        if (ordem.Coluna == "situacao")
        {
            return desc ? consulta.OrderByDescending(n => n.Status) : consulta.OrderBy(n => n.Status);
        }
        return null;
    }

    /// <summary>
    /// Volta a nota para "Recebida" e enfileira um novo lançamento no Bling.
    /// </summary>
    public async Task<Resultado> ReprocessarAsync(int notaId, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        NotaFiscal? nota = await _notas.Consultar()
            .Include(n => n.Parcelas)
            .FirstOrDefaultAsync(n => n.Id == notaId, cancelamento);

        // Nota de outro cliente responde igual a inexistente, para não revelar ids
        if (nota == null || !usuario.PodeAcessarCliente(nota.ClienteId))
        {
            return Resultado.NaoEncontrado(null);
        }
        if (nota.Status == StatusNota.Lancada || nota.Status == StatusNota.LancadaManualmente)
        {
            return Resultado.Invalido("A nota já foi lançada.");
        }
        if (nota.EmpresaId == null || nota.Parcelas.Count == 0)
        {
            return Resultado.Invalido("A nota não tem empresa ou parcelas para lançar.");
        }

        nota.Status = StatusNota.Recebida;
        nota.Tentativas = 0;
        nota.MensagemErro = null;
        await _notas.SalvarAsync(cancelamento);

        _jobs.Enqueue<LancamentoNotaJob>(j => j.LancarAsync(notaId, CancellationToken.None));
        return Resultado.Ok();
    }

    /// <summary>
    /// Marca a nota com problema como resolvida e lançada manualmente no Bling:
    /// sai das pendentes e do painel, guardando quem marcou, quando e a observação.
    /// </summary>
    public async Task<Resultado> MarcarLancadaManualmenteAsync(
        int notaId, string? observacao, ClaimsPrincipal usuario, CancellationToken cancelamento)
    {
        NotaFiscal? nota = await _notas.Consultar().FirstOrDefaultAsync(n => n.Id == notaId, cancelamento);

        // Nota de outro cliente responde igual a inexistente, para não revelar ids
        if (nota == null || !usuario.PodeAcessarCliente(nota.ClienteId))
        {
            return Resultado.NaoEncontrado(null);
        }
        if (!StatusComProblema.Todos.Contains(nota.Status))
        {
            return Resultado.Invalido("Só notas com problema podem ser marcadas como lançadas manualmente no Bling.");
        }

        string texto = (observacao ?? "").Trim();
        if (texto.Length > TamanhoMaximoObservacao)
        {
            return Resultado.Invalido("A observação pode ter no máximo " + TamanhoMaximoObservacao + " caracteres.");
        }

        nota.Status = StatusNota.LancadaManualmente;
        nota.ResolvidaManualmenteEm = DateTime.UtcNow;
        nota.ResolvidaManualmentePorId = usuario.UsuarioId();
        nota.ResolvidaManualmentePor = usuario.Identity?.Name;
        if (texto != "")
        {
            nota.ObservacaoResolucao = texto;
        }
        else
        {
            nota.ObservacaoResolucao = null;
        }

        await _notas.SalvarAsync(cancelamento);
        return Resultado.Ok();
    }
}
