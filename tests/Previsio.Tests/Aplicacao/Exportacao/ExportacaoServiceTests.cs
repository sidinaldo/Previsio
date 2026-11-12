using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Previsio.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Notas;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Exportacao;
using Previsio.Aplicacao.Sincronizacao;

namespace Previsio.Tests.Aplicacao.Exportacao;

public class ExportacaoServiceTests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    // 11/11/2026 12h UTC = 09h em Brasília
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();

    private Cliente _rede = null!;
    private Empresa _centro = null!;
    private Empresa _norte = null!;

    private ExportacaoService CriarServico()
    {
        var contas = ServicosDeTeste.ContasPagarService(_db, _relogio, Options.Create(new SincronizacaoOptions()));
        return new ExportacaoService(ServicosDeTeste.Notas(_db, new FilaDeJobsDeTeste()), contas, _relogio);
    }

    private async Task CadastrarRede()
    {
        _rede = new Cliente();
        _rede.Nome = "Rede";
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _centro = Empresa("Auto Center Centro", "11222333000262");
        _norte = Empresa("Auto Center Zona Norte", "11222333000181");
        await _db.SaveChangesAsync();
    }

    private Empresa Empresa(string nome, string cnpj)
    {
        var empresa = new Empresa();
        empresa.ClienteId = _rede.Id;
        empresa.Nome = nome;
        empresa.Cnpj = cnpj;
        _db.Empresas.Add(empresa);
        return empresa;
    }

    private NotaFiscal Nota(string numero, Empresa empresa, StatusNota status, DateTime recebidaEmUtc, params decimal[] parcelas)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = _rede.Id;
        nota.EmpresaId = empresa.Id;
        nota.Numero = numero;
        nota.Serie = "1";
        nota.ChaveAcesso = "3526" + numero;
        nota.NomeFornecedor = "ALFA DISTRIBUIDORA";
        nota.CnpjFornecedor = "45987123000104";
        nota.CnpjDestinatario = empresa.Cnpj;
        nota.DataEmissao = new DateTime(2026, 11, 10);
        nota.Status = status;
        nota.RecebidaEm = recebidaEmUtc;
        nota.ValorTotal = parcelas.Sum();
        for (int i = 0; i < parcelas.Length; i++)
        {
            var parcela = new ParcelaNota();
            parcela.NumeroParcela = (i + 1).ToString("000");
            parcela.Vencimento = new DateTime(2026, 11, 18).AddDays(30 * i);
            parcela.Valor = parcelas[i];
            parcela.BlingContaPagarId = 5000 + int.Parse(numero) * 10 + i;
            nota.Parcelas.Add(parcela);
        }
        _db.NotasFiscais.Add(nota);
        return nota;
    }

    private void Conta(Empresa empresa, long blingId, int diasAFrente, decimal valor, int situacao = SituacaoContaBling.EmAberto)
    {
        var conta = new ContaPagarSincronizada();
        conta.EmpresaId = empresa.Id;
        conta.BlingId = blingId;
        conta.Vencimento = new DateTime(2026, 11, 11).AddDays(diasAFrente);
        conta.Valor = valor;
        conta.Situacao = situacao;
        _db.ContasPagar.Add(conta);
    }

    /// <summary>
    /// Lê a aba da planilha como texto: cada célula vira o valor gravado (número ou texto) e o estilo.
    /// </summary>
    private static List<List<string>> Linhas(byte[] arquivo)
    {
        using (var zip = new ZipArchive(new MemoryStream(arquivo)))
        {
            Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
            Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
            Assert.NotNull(zip.GetEntry("xl/styles.xml"));
            XDocument aba = XDocument.Load(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            var linhas = new List<List<string>>();
            foreach (XElement linha in aba.Descendants(Ns + "row"))
            {
                var celulas = new List<string>();
                foreach (XElement celula in linha.Elements(Ns + "c"))
                {
                    string referencia = (string)celula.Attribute("r")!;
                    string valor = celula.Value;
                    celulas.Add(referencia + "=" + valor);
                }
                linhas.Add(celulas);
            }
            return linhas;
        }
    }

    [Fact]
    public void Referencia_das_colunas_segue_o_excel()
    {
        Assert.Equal("A1", PlanilhaXlsx.Referencia(0, 1));
        Assert.Equal("Z3", PlanilhaXlsx.Referencia(25, 3));
        Assert.Equal("AA10", PlanilhaXlsx.Referencia(26, 10));
        Assert.Equal("AZ2", PlanilhaXlsx.Referencia(51, 2));
    }

    [Fact]
    public void Planilha_grava_texto_dinheiro_e_data_como_valores_do_excel()
    {
        var planilha = new PlanilhaXlsx("Teste", new List<ColunaPlanilha>
        {
            new ColunaPlanilha("Nome", TipoColunaPlanilha.Texto, 10),
            new ColunaPlanilha("Valor", TipoColunaPlanilha.Dinheiro, 10),
            new ColunaPlanilha("Dia", TipoColunaPlanilha.Data, 10),
            new ColunaPlanilha("Quando", TipoColunaPlanilha.DataHora, 10)
        });
        planilha.AdicionarLinha("Óleo & filtro <5W30>\u0001", 1840.50m, new DateTime(2026, 11, 18), new DateTime(2026, 11, 18, 12, 0, 0));
        planilha.AdicionarLinha(null, null, null, null);

        List<List<string>> linhas = Linhas(planilha.Gerar());

        Assert.Equal(new List<string> { "A1=Nome", "B1=Valor", "C1=Dia", "D1=Quando" }, linhas[0]);
        // 18/11/2026 é o dia 46344 do Excel; meio-dia é ,5
        Assert.Equal(new List<string> { "A2=Óleo & filtro <5W30>", "B2=1840.50", "C2=46344", "D2=46344.5" }, linhas[1]);
        Assert.Empty(linhas[2]);
        Assert.Throws<ArgumentException>(() => planilha.AdicionarLinha("só uma"));
    }

    [Fact]
    public async Task Notas_viram_uma_linha_por_parcela_com_os_filtros_da_tela()
    {
        await CadastrarRede();
        Nota("100", _centro, StatusNota.Lancada, new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc), 500m, 340.50m);
        Nota("200", _norte, StatusNota.SemParcelas, new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc));
        Nota("300", _norte, StatusNota.Lancada, new DateTime(2026, 11, 9, 14, 0, 0, DateTimeKind.Utc), 99m);
        await _db.SaveChangesAsync();

        Resultado<ArquivoExportado> resultado = await CriarServico().NotasAsync(
            _rede.Id, null, null, null, null, "alfa", "numero", CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("previsio-notas-2026-11-11-0900.xlsx", resultado.Valor!.Nome);
        List<List<string>> linhas = Linhas(resultado.Valor.Conteudo);
        Assert.Equal(5, linhas.Count);
        Assert.Contains("B2=100", linhas[1]);
        Assert.Contains("J2=001", linhas[1]);
        Assert.Contains("L2=500", linhas[1]);
        Assert.Contains("M2=Lançada no Bling", linhas[1]);
        Assert.Contains("H2=11.222.333/0002-62", linhas[1]);
        // Recebida 10/11 13h UTC = 10h em Brasília
        Assert.Contains(linhas[1], c => c.StartsWith("A2=46336.4166666"));
        Assert.Contains("J3=002", linhas[2]);
        Assert.Contains("B4=200", linhas[3]);
        Assert.Contains("M4=Sem vencimentos no XML", linhas[3]);
        Assert.DoesNotContain(linhas[3], c => c.StartsWith("J4="));
        Assert.Contains("B5=300", linhas[4]);
    }

    [Fact]
    public async Task Pendentes_usa_o_filtro_de_pendentes_e_o_nome_do_arquivo()
    {
        await CadastrarRede();
        Nota("100", _centro, StatusNota.Lancada, new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc), 500m);
        Nota("200", _norte, StatusNota.Erro, new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc), 80m);
        await _db.SaveChangesAsync();

        Resultado<ArquivoExportado> resultado = await CriarServico().NotasAsync(
            _rede.Id, null, NotasService.SituacaoPendentes, null, null, null, "recebida", CancellationToken.None);

        Assert.StartsWith("previsio-notas-pendentes-", resultado.Valor!.Nome);
        List<List<string>> linhas = Linhas(resultado.Valor.Conteudo);
        Assert.Equal(2, linhas.Count);
        Assert.Contains("B2=200", linhas[1]);
        Assert.Contains("M2=Erro ao lançar", linhas[1]);
    }

    [Fact]
    public async Task Filtro_invalido_volta_a_mensagem()
    {
        await CadastrarRede();
        Resultado<ArquivoExportado> resultado = await CriarServico().NotasAsync(
            _rede.Id, null, "Inventada", null, null, null, null, CancellationToken.None);
        Assert.Equal(TipoResultado.Invalido, resultado.Tipo);
    }

    [Fact]
    public async Task Contas_a_pagar_em_aberto_do_periodo_com_a_nota_de_origem()
    {
        await CadastrarRede();
        Nota("100", _centro, StatusNota.Lancada, new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc), 500m);
        await _db.SaveChangesAsync();
        Conta(_centro, 6000, 7, 500m);
        Conta(_norte, 7777, 2, 120m, SituacaoContaBling.ParcialmentePago);
        Conta(_norte, 7778, 3, 999m, SituacaoContaBling.Pago);
        Conta(_norte, 7779, 31, 50m);
        Conta(_norte, 7780, -1, 50m);
        await _db.SaveChangesAsync();
        // Conta 6000 é a parcela 001 da NF 100 (BlingContaPagarId = 5000 + 100 * 10)

        Resultado<ArquivoExportado> resultado = await CriarServico().ContasPagarAsync(_rede.Id, 30, null, CancellationToken.None);

        List<List<string>> linhas = Linhas(resultado.Valor!.Conteudo);
        Assert.Equal(3, linhas.Count);
        Assert.Contains("B2=Auto Center Zona Norte", linhas[1]);
        Assert.Contains("E2=Parcialmente paga", linhas[1]);
        Assert.Contains("I2=Lançada direto no Bling", linhas[1]);
        Assert.Contains("D3=500", linhas[2]);
        Assert.Contains("F3=ALFA DISTRIBUIDORA", linhas[2]);
        Assert.Contains("G3=100", linhas[2]);
        Assert.Contains("I3=Nota lida pelo Previsio", linhas[2]);

        Resultado<ArquivoExportado> soNorte = await CriarServico().ContasPagarAsync(_rede.Id, 30, _norte.Id, CancellationToken.None);
        Assert.Equal(2, Linhas(soNorte.Valor!.Conteudo).Count);
    }

    [Fact]
    public async Task Contas_a_pagar_com_os_filtros_da_tela_traz_vencidas_e_o_fornecedor_das_lancadas_a_mao()
    {
        await CadastrarRede();
        Conta(_norte, 7780, -3, 50m);
        Conta(_norte, 7778, 3, 999m, SituacaoContaBling.Pago);
        await _db.SaveChangesAsync();
        _db.ContasPagar.Local.Single(c => c.BlingId == 7780).FornecedorNome = "Energia Elétrica";
        await _db.SaveChangesAsync();

        var filtro = new FiltroContas { Situacao = SituacaoFiltroContas.Abertas };
        Resultado<ArquivoExportado> resultado = await CriarServico().ContasPagarAsync(_rede.Id, filtro, CancellationToken.None);

        List<List<string>> linhas = Linhas(resultado.Valor!.Conteudo);
        Assert.Equal(2, linhas.Count);
        Assert.Contains("E2=Em aberto (vencida)", linhas[1]);
        Assert.Contains("F2=Energia Elétrica", linhas[1]);
        Assert.Contains("I2=Lançada direto no Bling", linhas[1]);

        Resultado<ArquivoExportado> todas = await CriarServico().ContasPagarAsync(_rede.Id, new FiltroContas(), CancellationToken.None);
        Assert.Equal(3, Linhas(todas.Valor!.Conteudo).Count);
    }
}
