using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Exportacao;
using Previsio.Aplicacao.Painel;
using Previsio.Core.Abstracoes;
using Previsio.Core.Comum;
using Previsio.Core.Entidades;
using Previsio.Core.Painel.Modelos;

namespace Previsio.Aplicacao.Relatorios;

/// <summary>
/// Dossiê financeiro da rede (tela Relatórios e o documento impresso): projeção de caixa de 30, 60 ou
/// 90 dias com as semanas críticas, saúde dos lançamentos no Bling e o comparativo do período por
/// empresa, fornecedor e categoria. Também a planilha com uma aba por bloco e as contas do período.
/// </summary>
public class DossieService
{
    public static readonly int[] HorizontesValidos = { 30, 60, 90 };
    public const int MaximoDiasPeriodo = 366;
    public const string MensagemHorizonte = "Escolha a projeção de 30, 60 ou 90 dias.";
    public const string MensagemPeriodo = "O período vai de uma data até outra igual ou depois, com no máximo 366 dias.";

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<ContaPagarSincronizada> _contas;
    private readonly IRepositorio<ContaReceberSincronizada> _contasReceber;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IRepositorio<CategoriaFinanceira> _categorias;
    private readonly PainelService _painel;
    private readonly RelatoriosOptions _opcoes;

    public DossieService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Empresa> empresas,
        IRepositorio<ContaPagarSincronizada> contas,
        IRepositorio<ContaReceberSincronizada> contasReceber,
        IRepositorio<NotaFiscal> notas,
        IRepositorio<CategoriaFinanceira> categorias,
        PainelService painel,
        IOptions<RelatoriosOptions> opcoes)
    {
        _clientes = clientes;
        _empresas = empresas;
        _contas = contas;
        _contasReceber = contasReceber;
        _notas = notas;
        _categorias = categorias;
        _painel = painel;
        _opcoes = opcoes.Value;
    }

    /// <summary>Sem período, vale o mês atual.</summary>
    public async Task<Resultado<DossieDto>> MontarAsync(
        int clienteId, int dias, DateTime? de, DateTime? ate, CancellationToken cancelamento)
    {
        Resultado<DossieCompleto> completo = await MontarCompletoAsync(clienteId, dias, de, ate, cancelamento);
        if (!completo.Sucesso)
        {
            return Falha<DossieDto>(completo);
        }
        return Resultado<DossieDto>.Ok(completo.Valor!.Dossie);
    }

    /// <summary>
    /// Planilha do dossiê: Projeção, Auditoria, Por empresa, Fornecedores, Categorias e as contas a pagar do período.
    /// </summary>
    public async Task<Resultado<ArquivoExportado>> ExportarAsync(
        int clienteId, int dias, DateTime? de, DateTime? ate, CancellationToken cancelamento)
    {
        Resultado<DossieCompleto> completo = await MontarCompletoAsync(clienteId, dias, de, ate, cancelamento);
        if (!completo.Sucesso)
        {
            return Falha<ArquivoExportado>(completo);
        }
        DossieDto dossie = completo.Valor!.Dossie;

        var abas = new List<PlanilhaXlsx>();
        abas.Add(PlanilhaDossie.Projecao(dossie.Projecao));
        abas.Add(PlanilhaDossie.Auditoria(dossie.Auditoria));
        abas.Add(PlanilhaDossie.Empresas(dossie.Comparativo));
        abas.Add(PlanilhaDossie.Ranking("Fornecedores", "Fornecedor", dossie.Comparativo.Fornecedores));
        abas.Add(PlanilhaDossie.Ranking("Categorias", "Categoria", dossie.Comparativo.Categorias));
        abas.Add(PlanilhaDossie.Contas(completo.Valor.ContasPeriodo, completo.Valor.NomesEmpresas, completo.Valor.NomesCategorias));

        var arquivo = new ArquivoExportado();
        arquivo.Nome = "previsio-dossie-" + dossie.De.ToString("yyyy-MM-dd") + "-a-" + dossie.Ate.ToString("yyyy-MM-dd") + ".xlsx";
        arquivo.Conteudo = PlanilhaXlsx.Gerar(abas);
        return Resultado<ArquivoExportado>.Ok(arquivo);
    }

    private async Task<Resultado<DossieCompleto>> MontarCompletoAsync(
        int clienteId, int dias, DateTime? de, DateTime? ate, CancellationToken cancelamento)
    {
        if (!HorizontesValidos.Contains(dias))
        {
            return Resultado<DossieCompleto>.Invalido(MensagemHorizonte);
        }
        DateTime hoje = RelogioBrasil.Hoje();
        DateTime inicio = new DateTime(hoje.Year, hoje.Month, 1);
        DateTime fim = inicio.AddMonths(1).AddDays(-1);
        if (de.HasValue)
        {
            inicio = de.Value.Date;
        }
        if (ate.HasValue)
        {
            fim = ate.Value.Date;
        }
        if (fim < inicio || (fim - inicio).TotalDays >= MaximoDiasPeriodo)
        {
            return Resultado<DossieCompleto>.Invalido(MensagemPeriodo);
        }

        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<DossieCompleto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        List<Empresa> empresas = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == clienteId && e.Ativo)
            .OrderBy(e => e.Nome)
            .ToListAsync(cancelamento);
        List<int> ids = empresas.Select(e => e.Id).ToList();

        var dossie = new DossieDto();
        dossie.RedeNome = cliente.Nome;
        dossie.GeradoEm = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, RelogioBrasil.Fuso);
        dossie.Dias = dias;
        dossie.De = inicio;
        dossie.Ate = fim;
        foreach (Empresa empresa in empresas)
        {
            dossie.Empresas.Add(new EmpresaDossieDto { Nome = empresa.Nome, Cnpj = empresa.Cnpj });
        }

        // 1) Projeção: as semanas do painel no horizonte escolhido
        ResumoPainel resumo = await _painel.ResumoAsync(clienteId, dias, cancelamento);
        dossie.Projecao = CalculoDossie.Projecao(resumo, _opcoes.FatorSemanaCriticaValido());

        // Contas a pagar do período (pagas e em aberto; devolvidas e canceladas não saem do caixa)
        DateTime fimDoDia = fim.AddDays(1);
        List<ContaPagarSincronizada> contasPeriodo = await _contas.ConsultarSomenteLeitura()
            .Where(c => ids.Contains(c.EmpresaId) && c.Vencimento >= inicio && c.Vencimento < fimDoDia
                && c.Situacao != SituacaoContaBling.Devolvido && c.Situacao != SituacaoContaBling.Cancelado)
            .ToListAsync(cancelamento);
        List<ContaReceberSincronizada> receberPeriodo = await _contasReceber.ConsultarSomenteLeitura()
            .Where(c => ids.Contains(c.EmpresaId) && c.Vencimento >= inicio && c.Vencimento < fimDoDia
                && c.Situacao != SituacaoContaBling.Devolvido && c.Situacao != SituacaoContaBling.Cancelado)
            .ToListAsync(cancelamento);

        // 2) Auditoria
        // RecebidaEm é UTC: o período (dias de Brasília) vira o intervalo em UTC
        DateTime recebidaDe = RelogioBrasil.InicioDoDiaEmUtc(inicio);
        DateTime recebidaAte = RelogioBrasil.InicioDoDiaEmUtc(fimDoDia);
        List<NotaFiscal> notasPeriodo = await _notas.ConsultarSomenteLeitura()
            .Where(n => n.ClienteId == clienteId && n.RecebidaEm >= recebidaDe && n.RecebidaEm < recebidaAte)
            .ToListAsync(cancelamento);
        List<ContaPagarSincronizada> emAberto = await _contas.ConsultarSomenteLeitura()
            .Where(c => ids.Contains(c.EmpresaId)
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago))
            .ToListAsync(cancelamento);

        var auditoria = new AuditoriaLancamentosDto();
        IndicadorConformidadeDto categorias = CalculoDossie.Categorias(contasPeriodo);
        categorias.Peso = RelatoriosOptions.PesoValido(_opcoes.PesoCategorias);
        IndicadorConformidadeDto notas = CalculoDossie.Notas(notasPeriodo);
        notas.Peso = RelatoriosOptions.PesoValido(_opcoes.PesoNotasLancadas);
        IndicadorConformidadeDto emDia = CalculoDossie.EmDia(emAberto, hoje);
        emDia.Peso = RelatoriosOptions.PesoValido(_opcoes.PesoContasEmDia);
        auditoria.Indicadores.Add(categorias);
        auditoria.Indicadores.Add(notas);
        auditoria.Indicadores.Add(emDia);
        auditoria.QuantidadeReceberVencido = resumo.QuantidadeReceberVencido;
        auditoria.ValorReceberVencido = resumo.TotalReceberVencido;
        CalculoDossie.AplicarIndice(auditoria, _opcoes.ConformidadeVerdeValida(), _opcoes.ConformidadeAmarelaValida());
        dossie.Auditoria = auditoria;

        // 3) Comparativo
        var nomesCategorias = new Dictionary<string, string>();
        var lista = await _categorias.ConsultarSomenteLeitura()
            .Where(k => ids.Contains(k.EmpresaId))
            .OrderBy(k => k.EmpresaId)
            .ThenBy(k => k.BlingId)
            .Select(k => new { k.EmpresaId, k.BlingId, k.Descricao })
            .ToListAsync(cancelamento);
        foreach (var categoria in lista)
        {
            nomesCategorias[categoria.EmpresaId + ":" + categoria.BlingId] = categoria.Descricao;
        }
        dossie.Comparativo = CalculoDossie.Comparativo(empresas, contasPeriodo, receberPeriodo, nomesCategorias, _opcoes.TamanhoRankingValido());

        var completo = new DossieCompleto();
        completo.Dossie = dossie;
        completo.ContasPeriodo = contasPeriodo.OrderBy(c => c.Vencimento).ThenBy(c => c.Id).ToList();
        completo.NomesCategorias = nomesCategorias;
        foreach (Empresa empresa in empresas)
        {
            completo.NomesEmpresas[empresa.Id] = empresa.Nome;
        }
        return Resultado<DossieCompleto>.Ok(completo);
    }

    private static Resultado<T> Falha<T>(Resultado<DossieCompleto> falha)
    {
        if (falha.Tipo == TipoResultado.NaoEncontrado)
        {
            return Resultado<T>.NaoEncontrado(falha.Mensagem);
        }
        return Resultado<T>.Invalido(falha.Mensagem ?? "");
    }
}
