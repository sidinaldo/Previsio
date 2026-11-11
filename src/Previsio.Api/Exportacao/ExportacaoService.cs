using Microsoft.EntityFrameworkCore;
using Previsio.Api.Servicos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Exportacao;

/// <summary>
/// Planilhas Excel das listas: notas (recebidas ou pendentes, uma linha por parcela) e contas a pagar em aberto.
/// Datas no horário de Brasília.
/// </summary>
public class ExportacaoService
{
    private readonly PrevisioDbContext _db;
    private readonly NotasService _notas;
    private readonly TimeProvider _relogio;

    public ExportacaoService(PrevisioDbContext db, NotasService notas, TimeProvider relogio)
    {
        _db = db;
        _notas = notas;
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
    public async Task<Resultado<ArquivoExportado>> ContasPagarAsync(
        int clienteId, int dias, int? empresaId, CancellationToken cancelamento)
    {
        dias = PainelService.NormalizarDias(dias);
        DateTime hoje = HoraBrasil(_relogio.GetUtcNow().UtcDateTime).Date;
        DateTime fim = hoje.AddDays(dias);

        IQueryable<Empresa> consultaEmpresas = _db.Empresas.AsNoTracking().Where(e => e.ClienteId == clienteId && e.Ativo);
        if (empresaId.HasValue)
        {
            consultaEmpresas = consultaEmpresas.Where(e => e.Id == empresaId.Value);
        }
        List<Empresa> empresas = await consultaEmpresas.ToListAsync(cancelamento);
        List<int> idsEmpresas = empresas.Select(e => e.Id).ToList();

        List<ContaPagarSincronizada> contas = await _db.ContasPagar.AsNoTracking()
            .Where(c => idsEmpresas.Contains(c.EmpresaId)
                && c.Vencimento >= hoje
                && c.Vencimento <= fim
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago))
            .OrderBy(c => c.Vencimento)
            .ThenBy(c => c.EmpresaId)
            .ThenBy(c => c.Id)
            .ToListAsync(cancelamento);

        // Parcela de nota que gerou a conta no Bling (o id da conta fica gravado na parcela)
        List<long> idsBling = contas.Select(c => c.BlingId).ToList();
        List<ParcelaNota> parcelas = await _db.ParcelasNota.AsNoTracking()
            .Include(p => p.NotaFiscal)
            .Where(p => p.BlingContaPagarId.HasValue && idsBling.Contains(p.BlingContaPagarId.Value) && p.NotaFiscal!.ClienteId == clienteId)
            .ToListAsync(cancelamento);

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

        foreach (ContaPagarSincronizada conta in contas)
        {
            Empresa? empresa = empresas.FirstOrDefault(e => e.Id == conta.EmpresaId);
            ParcelaNota? parcela = parcelas.FirstOrDefault(p => p.BlingContaPagarId == conta.BlingId && p.NotaFiscal != null && p.NotaFiscal.EmpresaId == conta.EmpresaId);
            string situacao = "Em aberto";
            if (conta.Situacao == SituacaoContaBling.ParcialmentePago)
            {
                situacao = "Parcialmente paga";
            }

            string? fornecedor = null;
            string? numero = null;
            string? numeroParcela = null;
            string origem = "Lançada direto no Bling";
            if (parcela != null)
            {
                fornecedor = parcela.NotaFiscal!.NomeFornecedor;
                numero = parcela.NotaFiscal.Numero;
                numeroParcela = parcela.NumeroParcela;
                origem = "Nota lida pelo Previsio";
            }

            planilha.AdicionarLinha(conta.Vencimento.Date, empresa == null ? "" : empresa.Nome, empresa == null ? "" : Cnpj(empresa.Cnpj),
                conta.Valor, situacao, fornecedor, numero, numeroParcela, origem);
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
