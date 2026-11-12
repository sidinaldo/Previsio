using Microsoft.EntityFrameworkCore;
using Previsio.Api.Contas;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;

namespace Previsio.Api.Exportacao;

/// <summary>
/// Planilhas Excel das listas: notas (recebidas ou pendentes, uma linha por parcela) e contas a pagar.
/// Datas no horário de Brasília.
/// </summary>
public class ExportacaoService
{
    private readonly NotasService _notas;
    private readonly ContasPagarService _contas;
    private readonly TimeProvider _relogio;

    public ExportacaoService(NotasService notas, ContasPagarService contas, TimeProvider relogio)
    {
        _notas = notas;
        _contas = contas;
        _relogio = relogio;
    }

    /// <summary>
    /// Notas com os filtros da tela, uma linha por parcela (a nota se repete). Nota sem parcela vira uma linha só.
    /// </summary>
    public async Task<Resultado<ArquivoExportado>> NotasAsync(
        int clienteId,
        int? empresaId,
        string? situacao,
        DateTime? de,
        DateTime? ate,
        string? busca,
        string? ordem,
        CancellationToken cancelamento)
    {
        Resultado<List<NotaFiscal>> consulta = await _notas.ListarParaExportarAsync(
            clienteId, empresaId, situacao, de, ate, busca, ordem, cancelamento);
        if (!consulta.Sucesso)
        {
            return Resultado<ArquivoExportado>.Invalido(consulta.Mensagem!);
        }

        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Recebida em", TipoColunaPlanilha.DataHora, 17),
            new ColunaPlanilha("NF", TipoColunaPlanilha.Texto, 10),
            new ColunaPlanilha("Série", TipoColunaPlanilha.Texto, 7),
            new ColunaPlanilha("Emissão", TipoColunaPlanilha.Data, 12),
            new ColunaPlanilha("Fornecedor", TipoColunaPlanilha.Texto, 36),
            new ColunaPlanilha("CNPJ do fornecedor", TipoColunaPlanilha.Texto, 20),
            new ColunaPlanilha("Empresa", TipoColunaPlanilha.Texto, 28),
            new ColunaPlanilha("CNPJ da empresa", TipoColunaPlanilha.Texto, 20),
            new ColunaPlanilha("Valor da nota", TipoColunaPlanilha.Dinheiro, 15),
            new ColunaPlanilha("Parcela", TipoColunaPlanilha.Texto, 9),
            new ColunaPlanilha("Vencimento", TipoColunaPlanilha.Data, 12),
            new ColunaPlanilha("Valor da parcela", TipoColunaPlanilha.Dinheiro, 16),
            new ColunaPlanilha("Situação", TipoColunaPlanilha.Texto, 24),
            new ColunaPlanilha("Lançada em", TipoColunaPlanilha.DataHora, 17),
            new ColunaPlanilha("Motivo", TipoColunaPlanilha.Texto, 40),
            new ColunaPlanilha("Chave de acesso", TipoColunaPlanilha.Texto, 48)
        };
        var planilha = new PlanilhaXlsx("Notas", colunas);

        foreach (NotaFiscal nota in consulta.Valor!)
        {
            string empresa = "";
            if (nota.Empresa != null)
            {
                empresa = nota.Empresa.Nome;
            }
            string? motivo = null;
            if (nota.Status != StatusNota.Lancada && nota.Status != StatusNota.LancadaManualmente)
            {
                motivo = nota.MensagemErro;
            }
            else if (nota.Status == StatusNota.LancadaManualmente)
            {
                motivo = nota.ObservacaoResolucao;
            }
            DateTime? lancadaEm = null;
            if (nota.LancadaEm.HasValue)
            {
                lancadaEm = HoraBrasil(nota.LancadaEm.Value);
            }
            else if (nota.ResolvidaManualmenteEm.HasValue)
            {
                lancadaEm = HoraBrasil(nota.ResolvidaManualmenteEm.Value);
            }

            List<ParcelaNota> parcelas = nota.Parcelas.OrderBy(p => p.Vencimento).ThenBy(p => p.NumeroParcela).ToList();
            if (parcelas.Count == 0)
            {
                planilha.AdicionarLinha(HoraBrasil(nota.RecebidaEm), nota.Numero, nota.Serie, nota.DataEmissao, nota.NomeFornecedor,
                    Cnpj(nota.CnpjFornecedor), empresa, Cnpj(nota.CnpjDestinatario), nota.ValorTotal,
                    null, null, null, TextoSituacaoNota.Descrever(nota.Status), lancadaEm, motivo, nota.ChaveAcesso);
            }
            foreach (ParcelaNota parcela in parcelas)
            {
                planilha.AdicionarLinha(HoraBrasil(nota.RecebidaEm), nota.Numero, nota.Serie, nota.DataEmissao, nota.NomeFornecedor,
                    Cnpj(nota.CnpjFornecedor), empresa, Cnpj(nota.CnpjDestinatario), nota.ValorTotal,
                    parcela.NumeroParcela, parcela.Vencimento, parcela.Valor, TextoSituacaoNota.Descrever(nota.Status),
                    lancadaEm, motivo, nota.ChaveAcesso);
            }
        }

        string prefixo = "notas";
        if (string.Equals(situacao, NotasService.SituacaoPendentes, StringComparison.OrdinalIgnoreCase))
        {
            prefixo = "notas-pendentes";
        }
        return Resultado<ArquivoExportado>.Ok(Arquivo(prefixo, planilha));
    }

    /// <summary>
    /// Contas a pagar em aberto no Bling (as mesmas do painel) dos próximos N dias, com a nota de origem
    /// quando a conta foi lançada pelo Previsio.
    /// </summary>
    public Task<Resultado<ArquivoExportado>> ContasPagarAsync(
        int clienteId, int dias, int? empresaId, CancellationToken cancelamento)
    {
        dias = PainelService.NormalizarDias(dias);
        DateTime hoje = _contas.Hoje();
        var filtro = new FiltroContas();
        filtro.EmpresaId = empresaId;
        filtro.De = hoje;
        filtro.Ate = hoje.AddDays(dias);
        filtro.Situacao = SituacaoFiltroContas.Abertas;
        return ContasPagarAsync(clienteId, filtro, cancelamento);
    }

    /// <summary>
    /// Contas a pagar com os filtros da tela Contas a pagar (lançadas pelo Previsio ou direto no Bling).
    /// </summary>
    public async Task<Resultado<ArquivoExportado>> ContasPagarAsync(
        int clienteId, FiltroContas filtro, CancellationToken cancelamento)
    {
        Resultado<List<LinhaContaPagar>> consulta = await _contas.ListarParaExportarAsync(clienteId, filtro, cancelamento);
        if (!consulta.Sucesso)
        {
            return Resultado<ArquivoExportado>.Invalido(consulta.Mensagem!);
        }

        var colunas = new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Vencimento", TipoColunaPlanilha.Data, 12),
            new ColunaPlanilha("Empresa", TipoColunaPlanilha.Texto, 28),
            new ColunaPlanilha("CNPJ da empresa", TipoColunaPlanilha.Texto, 20),
            new ColunaPlanilha("Valor", TipoColunaPlanilha.Dinheiro, 15),
            new ColunaPlanilha("Situação no Bling", TipoColunaPlanilha.Texto, 18),
            new ColunaPlanilha("Fornecedor", TipoColunaPlanilha.Texto, 36),
            new ColunaPlanilha("NF", TipoColunaPlanilha.Texto, 10),
            new ColunaPlanilha("Parcela", TipoColunaPlanilha.Texto, 9),
            new ColunaPlanilha("Origem", TipoColunaPlanilha.Texto, 26)
        };
        var planilha = new PlanilhaXlsx("Contas a pagar", colunas);

        DateTime hoje = _contas.Hoje();
        foreach (LinhaContaPagar linha in consulta.Valor!)
        {
            ContaPagarSincronizada conta = linha.Conta;
            string situacao = TextoSituacaoConta.De(conta.Situacao);
            if (TextoSituacaoConta.EmAberto(conta.Situacao) && conta.Vencimento.Date < hoje)
            {
                situacao = situacao + " (vencida)";
            }

            string? numero = null;
            string? numeroParcela = null;
            string origem = "Lançada direto no Bling";
            if (linha.Parcela != null)
            {
                numero = linha.Parcela.NumeroNota;
                numeroParcela = linha.Parcela.NumeroParcela;
                origem = "Nota lida pelo Previsio";
            }

            planilha.AdicionarLinha(conta.Vencimento.Date, linha.EmpresaNome, Cnpj(linha.EmpresaCnpj),
                conta.Valor, situacao, linha.Fornecedor, numero, numeroParcela, origem);
        }

        return Resultado<ArquivoExportado>.Ok(Arquivo("contas-a-pagar", planilha));
    }

    private ArquivoExportado Arquivo(string prefixo, PlanilhaXlsx planilha)
    {
        DateTime agora = HoraBrasil(_relogio.GetUtcNow().UtcDateTime);
        var arquivo = new ArquivoExportado();
        arquivo.Nome = "previsio-" + prefixo + "-" + agora.ToString("yyyy-MM-dd-HHmm") + ".xlsx";
        arquivo.Conteudo = planilha.Gerar();
        return arquivo;
    }

    private static DateTime HoraBrasil(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), RelogioBrasil.Fuso);
    }

    private static string Cnpj(string documento)
    {
        if (documento == null || documento.Length != 14)
        {
            return documento ?? "";
        }
        return documento.Substring(0, 2) + "." + documento.Substring(2, 3) + "." + documento.Substring(5, 3) + "/"
            + documento.Substring(8, 4) + "-" + documento.Substring(12, 2);
    }
}
