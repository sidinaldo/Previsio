using System.IO.Compression;
using System.Xml.Linq;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Aplicacao.Exportacao;
using Previsio.Aplicacao.Relatorios;
using Previsio.Core.Comum;
using Previsio.Core.Entidades;
using Previsio.Data;
using Previsio.Tests.Apoio;
using Xunit;

namespace Previsio.Tests.Aplicacao.Relatorios;

public class DossieServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly DateTime _hoje = RelogioBrasil.Hoje();
    private Cliente _rede = null!;
    private Empresa _matriz = null!;

    private async Task Cadastrar()
    {
        _rede = new Cliente { Nome = "Rede Auto Center" };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _matriz = new Empresa { ClienteId = _rede.Id, Nome = "Matriz", Cnpj = "11222333000181" };
        _db.Empresas.Add(_matriz);
        _db.Empresas.Add(new Empresa { ClienteId = _rede.Id, Nome = "Inativa", Cnpj = "11222333000262", Ativo = false });
        await _db.SaveChangesAsync();
        _db.Categorias.Add(new CategoriaFinanceira { EmpresaId = _matriz.Id, BlingId = 7, Descricao = "Compras de Mercadorias", Tipo = 1 });
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = _matriz.Id, BlingId = 1, Vencimento = _hoje.AddDays(2), Valor = 1000m, Situacao = SituacaoContaBling.EmAberto,
            FornecedorNome = "ALFA", CategoriaBlingId = 7, CategoriaConferidaEm = DateTime.UtcNow
        });
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = _matriz.Id, BlingId = 2, Vencimento = _hoje.AddDays(-3), Valor = 250m, Situacao = SituacaoContaBling.EmAberto,
            FornecedorNome = "BETA", CategoriaConferidaEm = DateTime.UtcNow
        });
        _db.ContasPagar.Add(new ContaPagarSincronizada
        {
            EmpresaId = _matriz.Id, BlingId = 3, Vencimento = _hoje.AddDays(1), Valor = 999m, Situacao = SituacaoContaBling.Cancelado
        });
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, ChaveAcesso = "1", Status = StatusNota.Lancada, RecebidaEm = _hoje, ValorTotal = 1000m });
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, ChaveAcesso = "2", Status = StatusNota.Erro, RecebidaEm = _hoje, ValorTotal = 80m });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Monta_os_tres_blocos_do_periodo()
    {
        await Cadastrar();

        Resultado<DossieDto> resultado = await ServicosDeTeste.Dossie(_db)
            .MontarAsync(_rede.Id, 30, _hoje.AddDays(-5), _hoje.AddDays(5), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        DossieDto dossie = resultado.Valor!;
        Assert.Equal("Rede Auto Center", dossie.RedeNome);
        Assert.Equal(new[] { "Matriz" }, dossie.Empresas.Select(e => e.Nome).ToArray());
        Assert.Equal(1000m, dossie.Projecao.TotalSaidas);
        Assert.Equal(250m, dossie.Projecao.SaidasVencidas);
        // Cancelada não entra
        Assert.Equal(1250m, dossie.Comparativo.TotalSaidas);
        Assert.Equal("ALFA", dossie.Comparativo.Fornecedores[0].Nome);
        IndicadorConformidadeDto notas = dossie.Auditoria.Indicadores.Single(i => i.Chave == "notas");
        Assert.Equal(50, notas.Percentual);
        Assert.Equal(1, notas.Quantidade);
        IndicadorConformidadeDto emDia = dossie.Auditoria.Indicadores.Single(i => i.Chave == "em-dia");
        Assert.Equal(80, emDia.Percentual);
        Assert.NotNull(dossie.Auditoria.Indice);
    }

    [Fact]
    public async Task Sem_periodo_usa_o_mes_atual_e_valida_horizonte_e_periodo()
    {
        await Cadastrar();
        DossieService servico = ServicosDeTeste.Dossie(_db);

        DossieDto mes = (await servico.MontarAsync(_rede.Id, 60, null, null, CancellationToken.None)).Valor!;
        Assert.Equal(new DateTime(_hoje.Year, _hoje.Month, 1), mes.De);
        Assert.Equal(new DateTime(_hoje.Year, _hoje.Month, 1).AddMonths(1).AddDays(-1), mes.Ate);

        Resultado<DossieDto> horizonte = await servico.MontarAsync(_rede.Id, 45, null, null, CancellationToken.None);
        Assert.Equal(DossieService.MensagemHorizonte, horizonte.Mensagem);
        Resultado<DossieDto> invertido = await servico.MontarAsync(_rede.Id, 30, _hoje, _hoje.AddDays(-1), CancellationToken.None);
        Assert.Equal(DossieService.MensagemPeriodo, invertido.Mensagem);
        Resultado<DossieDto> longo = await servico.MontarAsync(_rede.Id, 30, _hoje, _hoje.AddDays(400), CancellationToken.None);
        Assert.Equal(DossieService.MensagemPeriodo, longo.Mensagem);
        Resultado<DossieDto> semRede = await servico.MontarAsync(999, 30, null, null, CancellationToken.None);
        Assert.Equal(TipoResultado.NaoEncontrado, semRede.Tipo);
    }

    [Fact]
    public async Task Planilha_tem_uma_aba_por_bloco_e_as_contas_do_periodo()
    {
        await Cadastrar();

        Resultado<ArquivoExportado> resultado = await ServicosDeTeste.Dossie(_db)
            .ExportarAsync(_rede.Id, 30, _hoje.AddDays(-5), _hoje.AddDays(5), CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.StartsWith("previsio-dossie-", resultado.Valor!.Nome);
        using (var zip = new ZipArchive(new MemoryStream(resultado.Valor.Conteudo)))
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument pasta = XDocument.Load(zip.GetEntry("xl/workbook.xml")!.Open());
            Assert.Equal(new[] { "Projeção", "Auditoria", "Por empresa", "Fornecedores", "Categorias", "Contas do período" },
                pasta.Descendants(ns + "sheet").Select(s => (string)s.Attribute("name")!).ToArray());
            Assert.Equal(6, pasta.Descendants(ns + "definedName").Count());
            XDocument contas = XDocument.Load(zip.GetEntry("xl/worksheets/sheet6.xml")!.Open());
            string texto = string.Join("|", contas.Descendants(ns + "t").Select(t => t.Value));
            Assert.Contains("ALFA", texto);
            Assert.Contains("Compras de Mercadorias", texto);
            Assert.Contains("Sem categoria", texto);
            // Cabeçalho e 2 contas (a cancelada fica de fora)
            Assert.Equal(3, contas.Descendants(ns + "row").Count());
            string tipos = new StreamReader(zip.GetEntry("[Content_Types].xml")!.Open()).ReadToEnd();
            Assert.Contains("sheet6.xml", tipos);
        }
    }
}
