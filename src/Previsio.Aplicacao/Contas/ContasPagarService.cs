using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.FormasPagamento;
using Previsio.Core.Pagamento;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Tela "Contas a pagar": tudo o que a sincronização trouxe do Bling, lançado pelo Previsio ou à mão,
/// com fornecedor, situação e a nota de origem.
/// </summary>
public class ContasPagarService
{
    public const int MaximoExportacao = 20000;

    public const string MensagemExportacaoGrande =
        "São mais de 20.000 contas. Escolha um período menor ou uma empresa para exportar.";

    public const string MensagemOrdemInvalida =
        "Ordenação inválida. Use vencimento, valor, fornecedor ou empresa.";

    private readonly IRepositorio<ContaPagarSincronizada> _contasPagar;
    private readonly IRepositorio<ParcelaNota> _parcelas;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<CategoriaFinanceira> _categorias;
    private readonly IRepositorio<FormaPagamento> _formas;
    private readonly TimeProvider _relogio;
    private readonly SincronizacaoOptions _opcoes;
    private readonly List<string> _gruposSaiSozinho;

    public ContasPagarService(
        IRepositorio<ContaPagarSincronizada> contasPagar,
        IRepositorio<ParcelaNota> parcelas,
        IRepositorio<Empresa> empresas,
        IRepositorio<CategoriaFinanceira> categorias,
        IRepositorio<FormaPagamento> formas,
        TimeProvider relogio,
        IOptions<SincronizacaoOptions> opcoes,
        IOptions<FormasPagamentoOptions> opcoesFormas)
    {
        _formas = formas;
        _gruposSaiSozinho = opcoesFormas.Value.GruposSaiSozinhoValidos();
        _contasPagar = contasPagar;
        _parcelas = parcelas;
        _empresas = empresas;
        _categorias = categorias;
        _relogio = relogio;
        _opcoes = opcoes.Value;
    }

    public Task<Resultado<PaginaContasPagarDto>> ListarAsync(
        int clienteId, FiltroContas filtro, int pagina, CancellationToken cancelamento)
    {
        return ListarAsync(clienteId, filtro, pagina, null, cancelamento);
    }

    /// <summary>Uma página das contas; "tamanho" de 1 a 100 (sem ele, 25).</summary>
    public async Task<Resultado<PaginaContasPagarDto>> ListarAsync(
        int clienteId, FiltroContas filtro, int pagina, int? tamanho, CancellationToken cancelamento)
    {
        int tamanhoPagina = TamanhoPagina.Valido(tamanho);
        if (pagina < 1)
        {
            pagina = 1;
        }

        Resultado<IQueryable<ContaPagarSincronizada>> consulta = Filtrar(clienteId, filtro);
        if (!consulta.Sucesso)
        {
            return Resultado<PaginaContasPagarDto>.Invalido(consulta.Mensagem!);
        }

        int total = await consulta.Valor!.CountAsync(cancelamento);
        decimal valorTotal = await consulta.Valor!.SumAsync(c => c.Valor, cancelamento);

        Resultado<IOrderedQueryable<ContaPagarSincronizada>> ordenada = Ordenar(consulta.Valor!, filtro.Ordem);
        if (!ordenada.Sucesso)
        {
            return Resultado<PaginaContasPagarDto>.Invalido(ordenada.Mensagem!);
        }

        IQueryable<ContaPagarSincronizada> daPagina = ordenada.Valor!
            .ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina);
        List<LinhaContaPagar> linhas = await Projetar(daPagina).ToListAsync(cancelamento);
        MarcarSaiSozinho(linhas);

        DateTime hoje = Hoje();
        var resposta = new PaginaContasPagarDto();
        resposta.Total = total;
        resposta.Pagina = pagina;
        resposta.TamanhoPagina = tamanhoPagina;
        resposta.ValorTotal = valorTotal;
        resposta.DiasPassados = _opcoes.DiasPassadosValidos();
        resposta.DiasFuturos = _opcoes.DiasFuturosValidos();
        resposta.SincronizadaEm = await SincronizadaEmAsync(clienteId, filtro.EmpresaId, cancelamento);
        foreach (LinhaContaPagar linha in linhas)
        {
            resposta.Itens.Add(ParaDto(linha, hoje));
        }
        return Resultado<PaginaContasPagarDto>.Ok(resposta);
    }

    /// <summary>
    /// As mesmas contas da tela (filtros e ordem), todas de uma vez, para a planilha.
    /// </summary>
    public async Task<Resultado<List<LinhaContaPagar>>> ListarParaExportarAsync(
        int clienteId, FiltroContas filtro, CancellationToken cancelamento)
    {
        Resultado<IQueryable<ContaPagarSincronizada>> consulta = Filtrar(clienteId, filtro);
        if (!consulta.Sucesso)
        {
            return Resultado<List<LinhaContaPagar>>.Invalido(consulta.Mensagem!);
        }

        int total = await consulta.Valor!.CountAsync(cancelamento);
        if (total > MaximoExportacao)
        {
            return Resultado<List<LinhaContaPagar>>.Invalido(MensagemExportacaoGrande);
        }

        Resultado<IOrderedQueryable<ContaPagarSincronizada>> ordenada = Ordenar(consulta.Valor!, filtro.Ordem);
        if (!ordenada.Sucesso)
        {
            return Resultado<List<LinhaContaPagar>>.Invalido(ordenada.Mensagem!);
        }

        List<LinhaContaPagar> linhas = await Projetar(ordenada.Valor!.ThenBy(c => c.Id)).ToListAsync(cancelamento);
        MarcarSaiSozinho(linhas);
        return Resultado<List<LinhaContaPagar>>.Ok(linhas);
    }

    public DateTime Hoje()
    {
        DateTime utc = DateTime.SpecifyKind(_relogio.GetUtcNow().UtcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, RelogioBrasil.Fuso).Date;
    }

    private Resultado<IQueryable<ContaPagarSincronizada>> Filtrar(int clienteId, FiltroContas filtro)
    {
        IQueryable<ContaPagarSincronizada> consulta = _contasPagar.ConsultarSomenteLeitura()
            .Where(c => c.Empresa!.ClienteId == clienteId && c.Empresa.Ativo);

        if (filtro.EmpresaId.HasValue)
        {
            int empresaId = filtro.EmpresaId.Value;
            consulta = consulta.Where(c => c.EmpresaId == empresaId);
        }

        string? erroPeriodo = ValidacaoPeriodo.Validar(filtro.De, filtro.Ate);
        if (erroPeriodo != null)
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Invalido(erroPeriodo);
        }
        if (filtro.De.HasValue)
        {
            DateTime de = filtro.De.Value.Date;
            consulta = consulta.Where(c => c.Vencimento >= de);
        }
        if (filtro.Ate.HasValue)
        {
            DateTime ate = filtro.Ate.Value.Date;
            consulta = consulta.Where(c => c.Vencimento <= ate);
        }

        string situacao = (filtro.Situacao ?? "").Trim();
        if (situacao == "" || Igual(situacao, SituacaoFiltroContas.Todas))
        {
            // sem filtro
        }
        else if (Igual(situacao, SituacaoFiltroContas.Abertas))
        {
            consulta = consulta.Where(c => c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago);
        }
        else if (Igual(situacao, SituacaoFiltroContas.Vencidas))
        {
            DateTime hoje = Hoje();
            consulta = consulta.Where(c => (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago)
                && c.Vencimento < hoje);
        }
        else if (Igual(situacao, SituacaoFiltroContas.Pagas))
        {
            consulta = consulta.Where(c => c.Situacao == SituacaoContaBling.Pago);
        }
        else if (Igual(situacao, SituacaoFiltroContas.Canceladas))
        {
            consulta = consulta.Where(c => c.Situacao == SituacaoContaBling.Cancelado || c.Situacao == SituacaoContaBling.Devolvido);
        }
        else
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Invalido(
                "Situação inválida. Use Abertas, Vencidas, Pagas, Canceladas ou Todas.");
        }

        string origem = (filtro.Origem ?? "").Trim();
        if (origem == "")
        {
            // sem filtro
        }
        else if (Igual(origem, OrigemConta.Previsio))
        {
            consulta = consulta.Where(c => _parcelas.Consultar().Any(p => p.BlingContaPagarId == c.BlingId && p.NotaFiscal!.EmpresaId == c.EmpresaId));
        }
        else if (Igual(origem, OrigemConta.Bling))
        {
            consulta = consulta.Where(c => !_parcelas.Consultar().Any(p => p.BlingContaPagarId == c.BlingId && p.NotaFiscal!.EmpresaId == c.EmpresaId));
        }
        else
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Invalido("Origem inválida. Use Previsio ou Bling.");
        }

        string categoria = (filtro.Categoria ?? "").Trim();
        if (string.Equals(categoria, FiltroContas.SemCategoria, StringComparison.OrdinalIgnoreCase))
        {
            // Já conferidas no Bling e sem categoria
            consulta = consulta.Where(c => c.CategoriaConferidaEm != null && c.CategoriaBlingId == null);
        }
        else if (string.Equals(categoria, FiltroContas.CategoriaNaoIdentificada, StringComparison.OrdinalIgnoreCase))
        {
            // Têm categoria no Bling, mas ela não está na lista da empresa no Previsio
            consulta = consulta.Where(c => c.CategoriaBlingId != null
                && !_categorias.Consultar().Any(k => k.EmpresaId == c.EmpresaId && k.BlingId == c.CategoriaBlingId));
        }
        else if (categoria != "")
        {
            string nome = categoria.ToLower();
            consulta = consulta.Where(c => _categorias.Consultar()
                .Any(k => k.EmpresaId == c.EmpresaId && k.BlingId == c.CategoriaBlingId && k.Descricao.ToLower() == nome));
        }

        Resultado<IQueryable<ContaPagarSincronizada>> porForma = FiltrarForma(consulta, filtro.Forma);
        if (!porForma.Sucesso)
        {
            return porForma;
        }
        consulta = porForma.Valor!;

        BuscaTexto? termo = BuscaTexto.Ler(filtro.Busca);
        if (termo != null)
        {
            string texto = termo.Texto;
            consulta = consulta.Where(c =>
                (c.FornecedorNome != null && c.FornecedorNome.ToLower().Contains(texto))
                || c.Empresa!.Nome.ToLower().Contains(texto)
                || _parcelas.Consultar().Any(p => p.BlingContaPagarId == c.BlingId
                    && p.NotaFiscal!.EmpresaId == c.EmpresaId
                    && (p.NotaFiscal.Numero.Contains(texto) || p.NotaFiscal.NomeFornecedor.ToLower().Contains(texto))));
        }

        return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta);
    }

    public const string MensagemFormaInvalida =
        "Forma de pagamento inválida. Use boleto, pix, transferencia, cartao, dinheiro, debito-automatico, outras, sem, precisa-pagar ou sai-sozinho.";

    private Resultado<IQueryable<ContaPagarSincronizada>> FiltrarForma(IQueryable<ContaPagarSincronizada> consulta, string? forma)
    {
        string grupo = (forma ?? "").Trim().ToLowerInvariant();
        if (grupo == "")
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta);
        }
        List<string> saiSozinho = _gruposSaiSozinho;
        if (grupo == FiltroContas.SaiSozinho)
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta.Where(c => _formas.Consultar()
                .Any(f => f.EmpresaId == c.EmpresaId && f.BlingId == c.FormaPagamentoBlingId && saiSozinho.Contains(f.Grupo))));
        }
        if (grupo == FiltroContas.PrecisaPagar)
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta.Where(c => !_formas.Consultar()
                .Any(f => f.EmpresaId == c.EmpresaId && f.BlingId == c.FormaPagamentoBlingId && saiSozinho.Contains(f.Grupo))));
        }
        if (!GruposFormaPagamento.Valido(grupo))
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Invalido(MensagemFormaInvalida);
        }
        if (grupo == GruposFormaPagamento.Sem)
        {
            return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta.Where(c => c.FormaPagamentoBlingId == null));
        }
        if (grupo == GruposFormaPagamento.Outras)
        {
            // Outras inclui a forma que não está na lista da empresa no Previsio
            return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta.Where(c => c.FormaPagamentoBlingId != null
                && !_formas.Consultar().Any(f => f.EmpresaId == c.EmpresaId && f.BlingId == c.FormaPagamentoBlingId && f.Grupo != grupo)));
        }
        return Resultado<IQueryable<ContaPagarSincronizada>>.Ok(consulta.Where(c => _formas.Consultar()
            .Any(f => f.EmpresaId == c.EmpresaId && f.BlingId == c.FormaPagamentoBlingId && f.Grupo == grupo)));
    }

    private void MarcarSaiSozinho(List<LinhaContaPagar> linhas)
    {
        foreach (LinhaContaPagar linha in linhas)
        {
            linha.SaiSozinho = _gruposSaiSozinho.Contains(linha.FormaGrupo);
        }
    }

    /// <summary>
    /// Junta a empresa e a parcela de nota que gerou a conta (quando houver). Chamado depois de ordenar e paginar.
    /// </summary>
    private IQueryable<LinhaContaPagar> Projetar(IQueryable<ContaPagarSincronizada> consulta)
    {
        return consulta.Select(c => new LinhaContaPagar
        {
            Conta = c,
            EmpresaNome = c.Empresa!.Nome,
            EmpresaCnpj = c.Empresa.Cnpj,
            CategoriaNome = _categorias.Consultar()
                .Where(k => k.EmpresaId == c.EmpresaId && k.BlingId == c.CategoriaBlingId)
                .Select(k => k.Descricao)
                .FirstOrDefault(),
            FormaNome = _formas.Consultar()
                .Where(f => f.EmpresaId == c.EmpresaId && f.BlingId == c.FormaPagamentoBlingId)
                .Select(f => f.Descricao)
                .FirstOrDefault(),
            FormaGrupoGuardado = _formas.Consultar()
                .Where(f => f.EmpresaId == c.EmpresaId && f.BlingId == c.FormaPagamentoBlingId)
                .Select(f => f.Grupo)
                .FirstOrDefault(),
            Parcela = _parcelas.Consultar()
                .Where(p => p.BlingContaPagarId == c.BlingId && p.NotaFiscal!.EmpresaId == c.EmpresaId)
                .Select(p => new ParcelaDaConta
                {
                    NotaFiscalId = p.NotaFiscalId,
                    NumeroNota = p.NotaFiscal!.Numero,
                    NumeroParcela = p.NumeroParcela,
                    Fornecedor = p.NotaFiscal.NomeFornecedor
                })
                .FirstOrDefault()
        });
    }

    private static Resultado<IOrderedQueryable<ContaPagarSincronizada>> Ordenar(IQueryable<ContaPagarSincronizada> consulta, string? ordem)
    {
        OrdemLista escolhida = OrdemLista.Ler(ordem, "vencimento", false);
        bool desc = escolhida.Decrescente;
        if (escolhida.Coluna == "vencimento")
        {
            return Resultado<IOrderedQueryable<ContaPagarSincronizada>>.Ok(desc
                ? consulta.OrderByDescending(c => c.Vencimento).ThenBy(c => c.Empresa!.Nome)
                : consulta.OrderBy(c => c.Vencimento).ThenBy(c => c.Empresa!.Nome));
        }
        if (escolhida.Coluna == "valor")
        {
            return Resultado<IOrderedQueryable<ContaPagarSincronizada>>.Ok(desc
                ? consulta.OrderByDescending(c => c.Valor)
                : consulta.OrderBy(c => c.Valor));
        }
        if (escolhida.Coluna == "fornecedor")
        {
            return Resultado<IOrderedQueryable<ContaPagarSincronizada>>.Ok(desc
                ? consulta.OrderByDescending(c => c.FornecedorNome)
                : consulta.OrderBy(c => c.FornecedorNome));
        }
        if (escolhida.Coluna == "empresa")
        {
            return Resultado<IOrderedQueryable<ContaPagarSincronizada>>.Ok(desc
                ? consulta.OrderByDescending(c => c.Empresa!.Nome).ThenBy(c => c.Vencimento)
                : consulta.OrderBy(c => c.Empresa!.Nome).ThenBy(c => c.Vencimento));
        }
        return Resultado<IOrderedQueryable<ContaPagarSincronizada>>.Invalido(MensagemOrdemInvalida);
    }

    private async Task<DateTime?> SincronizadaEmAsync(int clienteId, int? empresaId, CancellationToken cancelamento)
    {
        IQueryable<Empresa> empresas = _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == clienteId && e.Ativo && e.BlingRefreshTokenProtegido != null);
        if (empresaId.HasValue)
        {
            int id = empresaId.Value;
            empresas = empresas.Where(e => e.Id == id);
        }
        List<DateTime?> datas = await empresas.Select(e => e.UltimaSincronizacaoEm).ToListAsync(cancelamento);
        DateTime? maisAntiga = null;
        foreach (DateTime? data in datas)
        {
            if (!data.HasValue)
            {
                continue;
            }
            if (!maisAntiga.HasValue || data.Value < maisAntiga.Value)
            {
                maisAntiga = data;
            }
        }
        return maisAntiga;
    }

    public static ContaPagarDto ParaDto(LinhaContaPagar linha, DateTime hoje)
    {
        ContaPagarSincronizada conta = linha.Conta;
        var dto = new ContaPagarDto();
        dto.Id = conta.Id;
        dto.BlingId = conta.BlingId;
        dto.EmpresaId = conta.EmpresaId;
        dto.EmpresaNome = linha.EmpresaNome;
        dto.Vencimento = conta.Vencimento.Date;
        dto.Valor = conta.Valor;
        dto.Situacao = conta.Situacao;
        dto.SituacaoTexto = TextoSituacaoConta.De(conta.Situacao);
        dto.Vencida = TextoSituacaoConta.EmAberto(conta.Situacao) && conta.Vencimento.Date < hoje;
        dto.Fornecedor = linha.Fornecedor;
        dto.Categoria = linha.Categoria;
        dto.FormaPagamento = linha.FormaNome;
        dto.FormaGrupo = linha.FormaGrupo;
        dto.FormaGrupoNome = GruposFormaPagamento.Nome(linha.FormaGrupo);
        dto.SaiSozinho = linha.SaiSozinho;
        dto.Origem = linha.Origem;
        if (linha.Parcela != null)
        {
            dto.NotaFiscalId = linha.Parcela.NotaFiscalId;
            dto.NumeroNota = linha.Parcela.NumeroNota;
            dto.NumeroParcela = linha.Parcela.NumeroParcela;
        }
        return dto;
    }

    private static bool Igual(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
