using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.BuscaSefaz;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Dtos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Infra.Seguranca;
using Previsio.Tests.Apoio;
using Previsio.Tests.Core.Sefaz;
using Xunit;

namespace Previsio.Tests.Aplicacao.BuscaSefaz;

public class BuscaCompletaSefazServiceTests
{
    // nfe-92001.xml: emitida para o Centro (11222333000262), duas duplicatas
    private const string ChaveExemplo = "31261171295348000100550040000920011289748350";
    private const string ChaveOutra = "31261171295348000100550040000920021289748351";
    private const string CnpjMatriz = "11222333000181";
    private const string CnpjCentro = "11222333000262";

    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private readonly SefazDeTeste _sefaz = new SefazDeTeste();
    private Cliente _rede = null!;
    private Empresa _matriz = null!;
    private Empresa _centro = null!;

    private DateTime Agora()
    {
        return _relogio.GetUtcNow().UtcDateTime;
    }

    // Matriz com certificado e UF; o Centro usa o da matriz (mesma raiz). As duas já começaram (NSU 100),
    // menos quando o teste diz o contrário
    private async Task CadastrarRede(string? nsuCentro = "000000000000100")
    {
        _rede = new Cliente { Nome = "Rede Auto Center" };
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _matriz = new Empresa { ClienteId = _rede.Id, Nome = "Zona Norte", Cnpj = CnpjMatriz, SefazUltimoNsu = "000000000000100" };
        _centro = new Empresa { ClienteId = _rede.Id, Nome = "Centro", Cnpj = CnpjCentro, Uf = "MG", SefazUltimoNsu = nsuCentro };
        _db.Empresas.AddRange(_matriz, _centro);
        await _db.SaveChangesAsync();
        Resultado<EmpresaDto> salvo = await ServicosDeTeste.Certificados(_db, _protetor).SalvarAsync(
            _matriz.Id, CertificadoDeTeste.Pfx(CnpjMatriz), CertificadoDeTeste.Senha, "RN", CancellationToken.None);
        Assert.True(salvo.Sucesso);
    }

    private BuscaCompletaSefazService Servico()
    {
        return new BuscaCompletaSefazService(
            new Repositorio<Cliente>(_db),
            new Repositorio<Empresa>(_db),
            new Repositorio<NotaFiscal>(_db),
            _sefaz,
            ServicosDeTeste.Certificados(_db, _protetor),
            ServicosDeTeste.RegistroNotaService(_db),
            new LeitorNFe(),
            _relogio,
            Options.Create(new BuscaSefazOptions()),
            NullLogger<BuscaCompletaSefazService>.Instance);
    }

    private static RetornoSefaz Lote(string ultimo, string maximo, params DocumentoDistribuido[] documentos)
    {
        RetornoSefaz retorno = SefazDeTeste.Retorno("138", "Documento localizado");
        retorno.UltimoNsu = ultimo;
        retorno.MaximoNsu = maximo;
        retorno.Documentos.AddRange(documentos);
        return retorno;
    }

    private static DocumentoDistribuido Completa(string nsu)
    {
        string xml = File.ReadAllText(ChaveETextoPdfTests.Exemplo("nfe-92001.xml"));
        return new DocumentoDistribuido { Nsu = nsu, Schema = "procNFe_v4.00.xsd", Xml = xml };
    }

    private static DocumentoDistribuido Resumo(string nsu, string chave, string situacao = "1")
    {
        string xml = "<resNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"1.01\"><chNFe>" + chave + "</chNFe>"
            + "<xNome>FORNECEDOR</xNome><vNF>100.00</vNF><cSitNFe>" + situacao + "</cSitNFe></resNFe>";
        return new DocumentoDistribuido { Nsu = nsu, Schema = "resNFe_v1.01.xsd", Xml = xml };
    }

    private static DocumentoDistribuido Cancelamento(string nsu, string chave)
    {
        string xml = "<procEventoNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"1.00\"><evento><infEvento>"
            + "<chNFe>" + chave + "</chNFe><tpEvento>110111</tpEvento></infEvento></evento></procEventoNFe>";
        return new DocumentoDistribuido { Nsu = nsu, Schema = "procEventoNFe_v1.00.xsd", Xml = xml };
    }

    [Fact]
    public async Task Primeira_busca_pula_o_historico_e_comeca_do_nsu_mais_novo()
    {
        await CadastrarRede(nsuCentro: null);
        _sefaz.ResponderDistribuicao(CnpjCentro, Lote("000000000000050", "000000000000500", Completa("000000000000010")));

        await Servico().ExecutarAsync(CancellationToken.None);

        Empresa centro = _db.Empresas.Single(e => e.Id == _centro.Id);
        Assert.Equal("000000000000500", centro.SefazUltimoNsu);
        Assert.Empty(_db.NotasFiscais);
        Assert.Contains("Busca ligada", centro.SefazSituacao);
        Assert.Equal(Agora().AddMinutes(60), centro.SefazProximaBuscaEm);
        Assert.Contains(_sefaz.Chamadas, c => c == "nsu " + CnpjCentro + " 0");
    }

    [Fact]
    public async Task Xml_completo_vira_nota_pelo_mesmo_caminho_do_email()
    {
        await CadastrarRede();
        _sefaz.ResponderDistribuicao(CnpjCentro, Lote("000000000000101", "000000000000101", Completa("000000000000101")));

        await Servico().ExecutarAsync(CancellationToken.None);

        NotaFiscal nota = Assert.Single(_db.NotasFiscais);
        Assert.Equal(ChaveExemplo, nota.ChaveAcesso);
        Assert.Equal(_centro.Id, nota.EmpresaId);
        Assert.Equal(StatusNota.Recebida, nota.Status);
        Assert.Equal(BuscaCompletaSefazService.OrigemSefaz, nota.OrigemEmail);
        Assert.Equal(2, nota.Parcelas.Count);
        Empresa centro = _db.Empresas.Single(e => e.Id == _centro.Id);
        Assert.Equal("000000000000101", centro.SefazUltimoNsu);
        Assert.Equal("1 nota nova.", centro.SefazSituacao);
    }

    [Fact]
    public async Task Xml_completo_em_que_a_empresa_nao_e_a_destinataria_fica_de_fora()
    {
        await CadastrarRede();
        // A nota do exemplo é do Centro; consultada pela matriz, ela aparece só como interessada
        _sefaz.ResponderDistribuicao(CnpjMatriz, Lote("000000000000101", "000000000000101", Completa("000000000000101")));

        await Servico().ExecutarAsync(CancellationToken.None);

        Assert.Empty(_db.NotasFiscais);
    }

    [Fact]
    public async Task Resumo_recebe_ciencia_menos_o_cancelado_e_o_que_ja_chegou_por_email()
    {
        await CadastrarRede();
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _centro.Id, ChaveAcesso = ChaveExemplo, Status = StatusNota.Lancada });
        await _db.SaveChangesAsync();
        string chaveCancelada = "31261171295348000100550040000920031289748352";
        _sefaz.ResponderDistribuicao(CnpjCentro, Lote("000000000000103", "000000000000103",
            Resumo("000000000000101", ChaveExemplo),
            Resumo("000000000000102", ChaveOutra),
            Resumo("000000000000103", chaveCancelada, "3")));

        await Servico().ExecutarAsync(CancellationToken.None);

        Assert.Equal(new[] { ChaveOutra }, _sefaz.Ciencias.ToArray());
        Empresa centro = _db.Empresas.Single(e => e.Id == _centro.Id);
        Assert.Equal("000000000000103", centro.SefazUltimoNsu);
        Assert.StartsWith("Ciência registrada em 1 nota", centro.SefazSituacao);
    }

    [Fact]
    public async Task Ciencia_com_consumo_indevido_para_no_documento_e_espera_uma_hora()
    {
        await CadastrarRede();
        _sefaz.RespostaCiencia = SefazDeTeste.Retorno("656", "Consumo indevido");
        _sefaz.ResponderDistribuicao(CnpjCentro, Lote("000000000000102", "000000000000102",
            Resumo("000000000000101", ChaveOutra),
            Resumo("000000000000102", "31261171295348000100550040000920041289748353")));

        await Servico().ExecutarAsync(CancellationToken.None);

        Empresa centro = _db.Empresas.Single(e => e.Id == _centro.Id);
        // Nenhum documento passou: recomeça do 101 depois de 1 hora
        Assert.Equal("000000000000100", centro.SefazUltimoNsu);
        Assert.Equal(Agora().AddHours(1), centro.SefazProximaBuscaEm);
    }

    [Fact]
    public async Task Consulta_com_consumo_indevido_espera_uma_hora_sem_mexer_no_nsu()
    {
        await CadastrarRede();
        _sefaz.ResponderDistribuicao(CnpjCentro, SefazDeTeste.Retorno("656", "Consumo indevido"));

        await Servico().ExecutarAsync(CancellationToken.None);

        Empresa centro = _db.Empresas.Single(e => e.Id == _centro.Id);
        Assert.Equal("000000000000100", centro.SefazUltimoNsu);
        Assert.Equal(Agora().AddHours(1), centro.SefazProximaBuscaEm);
        Assert.Contains("consumo indevido", centro.SefazSituacao);
    }

    [Fact]
    public async Task Cancelamento_tira_do_lancamento_a_nota_pendente_e_avisa_a_ja_lancada()
    {
        await CadastrarRede();
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _centro.Id, ChaveAcesso = ChaveExemplo, Status = StatusNota.Recebida });
        _db.NotasFiscais.Add(new NotaFiscal { ClienteId = _rede.Id, EmpresaId = _centro.Id, ChaveAcesso = ChaveOutra, Status = StatusNota.Lancada });
        await _db.SaveChangesAsync();
        _sefaz.ResponderDistribuicao(CnpjCentro, Lote("000000000000102", "000000000000102",
            Cancelamento("000000000000101", ChaveExemplo),
            Cancelamento("000000000000102", ChaveOutra)));

        await Servico().ExecutarAsync(CancellationToken.None);

        Assert.Equal(StatusNota.CanceladaNaSefaz, _db.NotasFiscais.Single(n => n.ChaveAcesso == ChaveExemplo).Status);
        Assert.Equal(StatusNota.Lancada, _db.NotasFiscais.Single(n => n.ChaveAcesso == ChaveOutra).Status);
        Empresa centro = _db.Empresas.Single(e => e.Id == _centro.Id);
        Assert.Contains("1 nota cancelada pelo emitente", centro.SefazSituacao);
        Assert.Contains("confira no Bling", centro.SefazSituacao);
    }

    [Fact]
    public async Task Respeita_o_intervalo_a_busca_desligada_e_a_empresa_sem_certificado()
    {
        await CadastrarRede();
        _centro.SefazProximaBuscaEm = Agora().AddMinutes(10);
        _matriz.SefazBuscaAtiva = false;
        var outraRaiz = new Empresa { ClienteId = _rede.Id, Nome = "Outra", Cnpj = "99888777000166", Uf = "SP" };
        _db.Empresas.Add(outraRaiz);
        await _db.SaveChangesAsync();

        await Servico().ExecutarAsync(CancellationToken.None);

        Assert.Empty(_sefaz.Chamadas);
    }

    [Fact]
    public async Task Com_mais_documentos_continua_lendo_ate_acabar()
    {
        await CadastrarRede();
        _sefaz.ResponderDistribuicao(CnpjCentro,
            Lote("000000000000150", "000000000000300", Resumo("000000000000150", ChaveOutra)),
            Lote("000000000000300", "000000000000300", Completa("000000000000300")));

        await Servico().ExecutarAsync(CancellationToken.None);

        Assert.Equal(2, _sefaz.Chamadas.Count(c => c.StartsWith("nsu " + CnpjCentro)));
        Assert.Single(_db.NotasFiscais);
        Assert.Equal("000000000000300", _db.Empresas.Single(e => e.Id == _centro.Id).SefazUltimoNsu);
    }

    [Fact]
    public async Task Religar_a_busca_recomeca_de_agora_em_diante()
    {
        await CadastrarRede();
        var certificados = ServicosDeTeste.Certificados(_db, _protetor);

        Resultado<EmpresaDto> desligada = await certificados.AlterarBuscaSefazAsync(_centro.Id, false, CancellationToken.None);
        Assert.False(desligada.Valor!.SefazBuscaAtiva);
        Assert.Equal("000000000000100", _db.Empresas.Single(e => e.Id == _centro.Id).SefazUltimoNsu);

        Resultado<EmpresaDto> religada = await certificados.AlterarBuscaSefazAsync(_centro.Id, true, CancellationToken.None);
        Assert.True(religada.Valor!.SefazBuscaAtiva);
        Assert.Null(_db.Empresas.Single(e => e.Id == _centro.Id).SefazUltimoNsu);
    }

    [Fact]
    public void Dono_do_certificado_e_a_propria_empresa_ou_a_da_mesma_raiz_na_rede()
    {
        var matriz = new Empresa { Id = 1, ClienteId = 1, Cnpj = CnpjMatriz, CertificadoPfxProtegido = "x", CertificadoSenhaProtegida = "y" };
        var filial = new Empresa { Id = 2, ClienteId = 1, Cnpj = CnpjCentro };
        var deOutraRede = new Empresa { Id = 3, ClienteId = 2, Cnpj = "11222333000343" };
        var lista = new List<Empresa> { matriz, filial, deOutraRede };

        Assert.Same(matriz, BuscaCompletaSefazService.DonoDoCertificado(matriz, lista));
        Assert.Same(matriz, BuscaCompletaSefazService.DonoDoCertificado(filial, lista));
        Assert.Null(BuscaCompletaSefazService.DonoDoCertificado(deOutraRede, lista));
    }

    [Fact]
    public void Configuracao_fora_dos_limites_volta_ao_padrao()
    {
        var opcoes = new BuscaSefazOptions { IntervaloMinutos = 30, MaximoConsultasPorExecucao = 0 };
        Assert.Equal(60, opcoes.IntervaloMinutosValido());
        Assert.Equal(10, opcoes.MaximoConsultasPorExecucaoValido());
        opcoes.IntervaloMinutos = 180;
        opcoes.MaximoConsultasPorExecucao = 20;
        Assert.Equal(180, opcoes.IntervaloMinutosValido());
        Assert.Equal(20, opcoes.MaximoConsultasPorExecucaoValido());
    }
}
