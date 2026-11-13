using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Comum;
using Previsio.Core.Entidades;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Sincronizacao;

namespace Previsio.Aplicacao.Contas;

/// <summary>
/// Tela "Contas a receber": o que a sincronização trouxe do Bling para o fluxo de caixa.
/// </summary>
public class ContasReceberService
{
    public const int TamanhoPagina = 50;
    public const int MaximoExportacao = 20000;

    public const string MensagemExportacaoGrande =
        "São mais de 20.000 contas. Escolha um período menor ou uma empresa para exportar.";

    public const string MensagemOrdemInvalida =
        "Ordenação inválida. Use vencimento, valor, cliente ou empresa.";

    private readonly IRepositorio<ContaReceberSincronizada> _contas;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly TimeProvider _relogio;
    private readonly SincronizacaoOptions _opcoes;

    public ContasReceberService(
        IRepositorio<ContaReceberSincronizada> contas,
        IRepositorio<Empresa> empresas,
        IRepositorio<Cliente> clientes,
        TimeProvider relogio,
        IOptions<SincronizacaoOptions> opcoes)
    {
        _contas = contas;
        _empresas = empresas;
        _clientes = clientes;
        _relogio = relogio;
        _opcoes = opcoes.Value;
    }

    public async Task<Resultado<PaginaContasReceberDto>> ListarAsync(
        int clienteId, FiltroContasReceber filtro, int pagina, CancellationToken cancelamento)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        Resultado<IQueryable<ContaReceberSincronizada>> consulta = Filtrar(clienteId, filtro);
        if (!consulta.Sucesso)
        {
            return Resultado<PaginaContasReceberDto>.Invalido(consulta.Mensagem!);
        }

        int total = await consulta.Valor!.CountAsync(cancelamento);
        decimal valorTotal = await consulta.Valor!.SumAsync(c => c.Valor, cancelamento);

        Resultado<IOrderedQueryable<ContaReceberSincronizada>> ordenada = Ordenar(consulta.Valor!, filtro.Ordem);
        if (!ordenada.Sucesso)
        {
            return Resultado<PaginaContasReceberDto>.Invalido(ordenada.Mensagem!);
        }

        List<LinhaContaReceber> linhas = await Projetar(ordenada.Valor!
                .ThenBy(c => c.Id)
                .Skip((pagina - 1) * TamanhoPagina)
                .Take(TamanhoPagina))
            .ToListAsync(cancelamento);

        DateTime hoje = Hoje();
        var resposta = new PaginaContasReceberDto();
        resposta.Total = total;
        resposta.Pagina = pagina;
        resposta.TamanhoPagina = TamanhoPagina;
        resposta.ValorTotal = valorTotal;
        resposta.DiasPassados = _opcoes.DiasPassadosValidos();
        resposta.DiasFuturos = _opcoes.DiasFuturosValidos();
        resposta.SincronizadaEm = await SincronizadaEmAsync(clienteId, filtro.EmpresaId, cancelamento);
        resposta.FluxoCaixaAtivo = await _clientes.ConsultarSomenteLeitura()
            .Where(c => c.Id == clienteId)
            .Select(c => c.MostrarFluxoCaixa)
            .FirstOrDefaultAsync(cancelamento);
        foreach (LinhaContaReceber linha in linhas)
        {
            resposta.Itens.Add(ParaDto(linha, hoje));
        }
        return Resultado<PaginaContasReceberDto>.Ok(resposta);
    }

    /// <summary>
    /// As mesmas contas da tela (filtros e ordem), todas de uma vez, para a planilha.
    /// </summary>
    public async Task<Resultado<List<LinhaContaReceber>>> ListarParaExportarAsync(
        int clienteId, FiltroContasReceber filtro, CancellationToken cancelamento)
    {
        Resultado<IQueryable<ContaReceberSincronizada>> consulta = Filtrar(clienteId, filtro);
        if (!consulta.Sucesso)
        {
            return Resultado<List<LinhaContaReceber>>.Invalido(consulta.Mensagem!);
        }

        int total = await consulta.Valor!.CountAsync(cancelamento);
        if (total > MaximoExportacao)
        {
            return Resultado<List<LinhaContaReceber>>.Invalido(MensagemExportacaoGrande);
        }

        Resultado<IOrderedQueryable<ContaReceberSincronizada>> ordenada = Ordenar(consulta.Valor!, filtro.Ordem);
        if (!ordenada.Sucesso)
        {
            return Resultado<List<LinhaContaReceber>>.Invalido(ordenada.Mensagem!);
        }

        List<LinhaContaReceber> linhas = await Projetar(ordenada.Valor!.ThenBy(c => c.Id)).ToListAsync(cancelamento);
        return Resultado<List<LinhaContaReceber>>.Ok(linhas);
    }

    public DateTime Hoje()
    {
        DateTime utc = DateTime.SpecifyKind(_relogio.GetUtcNow().UtcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, RelogioBrasil.Fuso).Date;
    }

    private Resultado<IQueryable<ContaReceberSincronizada>> Filtrar(int clienteId, FiltroContasReceber filtro)
    {
        IQueryable<ContaReceberSincronizada> consulta = _contas.ConsultarSomenteLeitura()
            .Where(c => c.Empresa!.ClienteId == clienteId && c.Empresa.Ativo);

        if (filtro.EmpresaId.HasValue)
        {
            int empresaId = filtro.EmpresaId.Value;
            consulta = consulta.Where(c => c.EmpresaId == empresaId);
        }

        string? erroPeriodo = ValidacaoPeriodo.Validar(filtro.De, filtro.Ate);
        if (erroPeriodo != null)
        {
            return Resultado<IQueryable<ContaReceberSincronizada>>.Invalido(erroPeriodo);
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
        else if (Igual(situacao, SituacaoFiltroContas.Recebidas))
        {
            consulta = consulta.Where(c => c.Situacao == SituacaoContaBling.Pago);
        }
        else if (Igual(situacao, SituacaoFiltroContas.Canceladas))
        {
            consulta = consulta.Where(c => c.Situacao == SituacaoContaBling.Cancelado || c.Situacao == SituacaoContaBling.Devolvido);
        }
        else
        {
            return Resultado<IQueryable<ContaReceberSincronizada>>.Invalido(
                "Situação inválida. Use Abertas, Vencidas, Recebidas, Canceladas ou Todas.");
        }

        BuscaTexto? termo = BuscaTexto.Ler(filtro.Busca);
        if (termo != null)
        {
            string texto = termo.Texto;
            consulta = consulta.Where(c =>
                (c.ClienteNome != null && c.ClienteNome.ToLower().Contains(texto))
                || c.Empresa!.Nome.ToLower().Contains(texto));
        }

        return Resultado<IQueryable<ContaReceberSincronizada>>.Ok(consulta);
    }

    private static IQueryable<LinhaContaReceber> Projetar(IQueryable<ContaReceberSincronizada> consulta)
    {
        return consulta.Select(c => new LinhaContaReceber
        {
            Conta = c,
            EmpresaNome = c.Empresa!.Nome,
            EmpresaCnpj = c.Empresa.Cnpj
        });
    }

    private static Resultado<IOrderedQueryable<ContaReceberSincronizada>> Ordenar(
        IQueryable<ContaReceberSincronizada> consulta, string? ordem)
    {
        OrdemLista escolhida = OrdemLista.Ler(ordem, "vencimento", false);
        bool desc = escolhida.Decrescente;
        IOrderedQueryable<ContaReceberSincronizada> ordenada;
        if (escolhida.Coluna == "vencimento")
        {
            if (desc)
            {
                ordenada = consulta.OrderByDescending(c => c.Vencimento).ThenBy(c => c.Empresa!.Nome);
            }
            else
            {
                ordenada = consulta.OrderBy(c => c.Vencimento).ThenBy(c => c.Empresa!.Nome);
            }
        }
        else if (escolhida.Coluna == "valor")
        {
            if (desc)
            {
                ordenada = consulta.OrderByDescending(c => c.Valor);
            }
            else
            {
                ordenada = consulta.OrderBy(c => c.Valor);
            }
        }
        else if (escolhida.Coluna == "cliente")
        {
            if (desc)
            {
                ordenada = consulta.OrderByDescending(c => c.ClienteNome);
            }
            else
            {
                ordenada = consulta.OrderBy(c => c.ClienteNome);
            }
        }
        else if (escolhida.Coluna == "empresa")
        {
            if (desc)
            {
                ordenada = consulta.OrderByDescending(c => c.Empresa!.Nome).ThenBy(c => c.Vencimento);
            }
            else
            {
                ordenada = consulta.OrderBy(c => c.Empresa!.Nome).ThenBy(c => c.Vencimento);
            }
        }
        else
        {
            return Resultado<IOrderedQueryable<ContaReceberSincronizada>>.Invalido(MensagemOrdemInvalida);
        }
        return Resultado<IOrderedQueryable<ContaReceberSincronizada>>.Ok(ordenada);
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

    public static ContaReceberDto ParaDto(LinhaContaReceber linha, DateTime hoje)
    {
        ContaReceberSincronizada conta = linha.Conta;
        var dto = new ContaReceberDto();
        dto.Id = conta.Id;
        dto.BlingId = conta.BlingId;
        dto.EmpresaId = conta.EmpresaId;
        dto.EmpresaNome = linha.EmpresaNome;
        dto.Vencimento = conta.Vencimento.Date;
        dto.Valor = conta.Valor;
        dto.Situacao = conta.Situacao;
        dto.SituacaoTexto = TextoSituacaoConta.DeReceber(conta.Situacao);
        dto.Vencida = TextoSituacaoConta.EmAberto(conta.Situacao) && conta.Vencimento.Date < hoje;
        dto.Cliente = conta.ClienteNome;
        return dto;
    }

    private static bool Igual(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
