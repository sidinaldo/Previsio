using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Previsio.Core.Entidades;
using Previsio.Core.Abstracoes;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Mapeamentos;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Autenticacao;

namespace Previsio.Aplicacao.Notas;

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
    private readonly IFilaJobs _jobs;

    public NotasService(IRepositorio<NotaFiscal> notas, IFilaJobs jobs)
    {
        _notas = notas;
        _jobs = jobs;
    }

    /// <summary>
    /// Quantas notas ainda não viraram conta a pagar no Bling (o número ao lado de "Notas pendentes" no menu).
    /// </summary>
    public Task<int> ContarPendentesAsync(int clienteId, CancellationToken cancelamento)
    {
        return _notas.ConsultarSomenteLeitura()
            .CountAsync(n => n.ClienteId == clienteId && StatusPendentes.Todos.Contains(n.Status), cancelamento);
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

        Resultado<IOrderedQueryable<NotaFiscal>> filtro = Filtrar(clienteId, empresaId, situacao, de, ate, busca, ordem);
        if (!filtro.Sucesso)
        {
            return Resultado<PaginaNotasDto>.Invalido(filtro.Mensagem!);
        }
        IOrderedQueryable<NotaFiscal> ordenada = filtro.Valor!;

        int total = await ordenada.CountAsync(cancelamento);

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

    public const int MaximoExportacao = 20000;

    public const string MensagemExportacaoGrande =
        "São mais de 20.000 notas. Escolha um período menor ou uma empresa para exportar.";

    /// <summary>
    /// As mesmas notas da listagem (mesmos filtros, busca e ordem), todas de uma vez e com as parcelas, para a planilha.
    /// </summary>
    public async Task<Resultado<List<NotaFiscal>>> ListarParaExportarAsync(
        int clienteId,
        int? empresaId,
        string? situacao,
        DateTime? de,
        DateTime? ate,
        string? busca,
        string? ordem,
        CancellationToken cancelamento)
    {
        Resultado<IOrderedQueryable<NotaFiscal>> filtro = Filtrar(clienteId, empresaId, situacao, de, ate, busca, ordem);
        if (!filtro.Sucesso)
        {
            return Resultado<List<NotaFiscal>>.Invalido(filtro.Mensagem!);
        }

        List<NotaFiscal> notas = await filtro.Valor!
            .ThenByDescending(n => n.Id)
            .Include(n => n.Empresa)
            .Include(n => n.Parcelas)
            .Take(MaximoExportacao + 1)
            .ToListAsync(cancelamento);
        if (notas.Count > MaximoExportacao)
        {
            return Resultado<List<NotaFiscal>>.Invalido(MensagemExportacaoGrande);
        }
        return Resultado<List<NotaFiscal>>.Ok(notas);
    }

    /// <summary>
    /// Consulta das notas do cliente com os filtros da tela e a ordem pedida.
    /// </summary>
    private Resultado<IOrderedQueryable<NotaFiscal>> Filtrar(
        int clienteId,
        int? empresaId,
        string? situacao,
        DateTime? de,
        DateTime? ate,
        string? busca,
        string? ordem)
    {
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
                    return Resultado<IOrderedQueryable<NotaFiscal>>.Invalido("Situação inválida.");
                }
                consulta = consulta.Where(n => n.Status == escolhida);
            }
        }

        string? erroPeriodo = ValidacaoPeriodo.Validar(de, ate);
        if (erroPeriodo != null)
        {
            return Resultado<IOrderedQueryable<NotaFiscal>>.Invalido(erroPeriodo);
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
            return Resultado<IOrderedQueryable<NotaFiscal>>.Invalido(MensagemOrdemInvalida);
        }
        return Resultado<IOrderedQueryable<NotaFiscal>>.Ok(ordenada);
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

        _jobs.LancarNota(notaId);
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
