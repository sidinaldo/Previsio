using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Core.Painel;
using Previsio.Data;
using Previsio.Data.Repositorios;
using Previsio.Infra.Seguranca;
using Previsio.Aplicacao.Abstracoes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Previsio.Core.NFe;
using Previsio.Aplicacao.Empresas;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Categorias;
using Previsio.Aplicacao.Clientes;
using Previsio.Aplicacao.Falhas;
using Previsio.Aplicacao.Notas;
using Previsio.Aplicacao.Painel;
using Previsio.Aplicacao.Usuarios;
using Previsio.Aplicacao.Certificados;
using Previsio.Aplicacao.Saude;
using Previsio.Aplicacao.Autenticacao;
using Previsio.Aplicacao.Avisos;
using Previsio.Aplicacao.Conferencia;
using Previsio.Aplicacao.Contas;
using Previsio.Aplicacao.Contratos;
using Previsio.Aplicacao.EmailsIgnorados;
using Previsio.Aplicacao.NotasSoPdf;
using Previsio.Aplicacao.Resumos;
using Previsio.Aplicacao.Sincronizacao;
using Previsio.Aplicacao.Vencimentos;
using Previsio.Aplicacao.Comum;

namespace Previsio.Tests.Apoio;

/// <summary>
/// Monta os serviços da API sobre o banco em memória, do mesmo jeito que o DI faz.
/// </summary>
public static class ServicosDeTeste
{
    public static AcessoEmpresaService Acesso(PrevisioDbContext db)
    {
        return new AcessoEmpresaService(new Repositorio<Empresa>(db));
    }

    public static ClientesService Clientes(PrevisioDbContext db, ProtetorSegredos protetor)
    {
        return new ClientesService(new Repositorio<Cliente>(db), new Repositorio<Empresa>(db), protetor);
    }

    public static EmpresasService Empresas(PrevisioDbContext db, ProtetorSegredos protetor)
    {
        return new EmpresasService(new Repositorio<Empresa>(db), new Repositorio<Cliente>(db), Acesso(db), protetor);
    }

    public static CertificadosService Certificados(PrevisioDbContext db, ProtetorSegredos protetor)
    {
        return CertificadosService(db, protetor, TimeProvider.System);
    }

    public static AutenticacaoService Autenticacao(PrevisioDbContext db)
    {
        return AutenticacaoService(db, new HashSenhaIdentity(), NullLogger<AutenticacaoService>.Instance);
    }

    public static UsuariosService Usuarios(PrevisioDbContext db)
    {
        return new UsuariosService(new Repositorio<Usuario>(db), new Repositorio<Cliente>(db), Autenticacao(db));
    }

    public static PainelService Painel(PrevisioDbContext db)
    {
        return new PainelService(
            new Repositorio<Empresa>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            new Repositorio<NotaFiscal>(db),
            new Repositorio<ContaReceberSincronizada>(db),
            new Repositorio<Cliente>(db),
            new Repositorio<CategoriaFinanceira>(db),
            new CalculadoraPainel());
    }

    public static NotasService Notas(PrevisioDbContext db, IFilaJobs fila)
    {
        return new NotasService(new Repositorio<NotaFiscal>(db), fila);
    }

    public static FalhasService Falhas(PrevisioDbContext db)
    {
        return new FalhasService(new Repositorio<FalhaProcessamento>(db));
    }

    public static BlingConexaoService ConexaoBling(
        PrevisioDbContext db, ProtetorSegredos protetor, BlingClient bling, IFilaJobs fila)
    {
        var tokens = TokenBlingService(bling, db, protetor, NullLogger<TokenBlingService>.Instance);
        return new BlingConexaoService(
            new Repositorio<Empresa>(db),
            Acesso(db),
            bling,
            tokens,
            fila,
            new ProtetorTemporario(new EphemeralDataProtectionProvider()),
            NullLogger<BlingConexaoService>.Instance);
    }

    // Serviços que usam repositórios: montam um Repositorio<T> por tabela sobre o mesmo contexto

    public static CertificadosService CertificadosService(
        PrevisioDbContext db,
        IProtetorSegredos protetor,
        TimeProvider relogio)
    {
        return new CertificadosService(
            new Repositorio<Empresa>(db),
            new Repositorio<NotaSoPdf>(db),
            protetor,
            relogio);
    }

    public static ContasPagarService ContasPagarService(
        PrevisioDbContext db,
        TimeProvider relogio,
        IOptions<SincronizacaoOptions> opcoes)
    {
        return new ContasPagarService(
            new Repositorio<ContaPagarSincronizada>(db),
            new Repositorio<ParcelaNota>(db),
            new Repositorio<Empresa>(db),
            new Repositorio<CategoriaFinanceira>(db),
            relogio,
            opcoes);
    }

    public static ContasReceberService ContasReceberService(
        PrevisioDbContext db,
        TimeProvider relogio,
        IOptions<SincronizacaoOptions> opcoes)
    {
        return new ContasReceberService(
            new Repositorio<ContaReceberSincronizada>(db),
            new Repositorio<Empresa>(db),
            new Repositorio<Cliente>(db),
            relogio,
            opcoes);
    }

    public static AutenticacaoService AutenticacaoService(
        PrevisioDbContext db,
        IHashSenha hasher,
        ILogger<AutenticacaoService> logger)
    {
        return new AutenticacaoService(
            new Repositorio<Usuario>(db),
            hasher,
            logger);
    }

    public static RedefinicaoSenhaService RedefinicaoSenhaService(
        PrevisioDbContext db,
        AutenticacaoService autenticacao,
        IEnvioEmail email,
        IConfiguration configuracao,
        AmbienteAplicacao ambiente,
        TimeProvider relogio,
        ILogger<RedefinicaoSenhaService> logger)
    {
        return new RedefinicaoSenhaService(
            new Repositorio<Usuario>(db),
            autenticacao,
            email,
            configuracao,
            ambiente,
            relogio,
            logger);
    }

    public static ConfiguracaoConferenciaService ConfiguracaoConferenciaService(PrevisioDbContext db)
    {
        return new ConfiguracaoConferenciaService(
            new Repositorio<Cliente>(db));
    }

    public static ConferenciaBlingService ConferenciaBlingService(PrevisioDbContext db, BlingClient bling)
    {
        return new ConferenciaBlingService(
            new Repositorio<Cliente>(db),
            new Repositorio<NotaFiscal>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            new Repositorio<ParcelaNota>(db),
            bling);
    }

    public static AlertaVencimentosService AlertaVencimentosService(
        PrevisioDbContext db,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<AlertaVencimentosService> logger)
    {
        return new AlertaVencimentosService(
            new Repositorio<Cliente>(db),
            new Repositorio<Usuario>(db),
            new Repositorio<Empresa>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            email,
            configuracao,
            relogio,
            logger);
    }

    public static LancamentoNotaService LancamentoNotaService(
        PrevisioDbContext db,
        BlingClient bling,
        TokenBlingService tokens,
        ConferenciaBlingService conferencia)
    {
        return new LancamentoNotaService(
            new Repositorio<NotaFiscal>(db),
            new Repositorio<Cliente>(db),
            new Repositorio<FalhaProcessamento>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            new CategoriaLancamentoService(
                new Repositorio<RegraCategoria>(db),
                new Repositorio<Cliente>(db),
                new Repositorio<ContaPagarSincronizada>(db),
                new Repositorio<CategoriaFinanceira>(db),
                new CategoriasBlingService(new Repositorio<CategoriaFinanceira>(db), bling, NullLogger<CategoriasBlingService>.Instance)),
            bling,
            tokens,
            conferencia,
            NullLogger<LancamentoNotaService>.Instance);
    }

    public static SincronizacaoContasService SincronizacaoContasService(
        PrevisioDbContext db,
        BlingClient bling,
        TokenBlingService tokens,
        IOptions<SincronizacaoOptions> opcoes)
    {
        return new SincronizacaoContasService(
            new Repositorio<Cliente>(db),
            new Repositorio<Empresa>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            new Repositorio<FalhaProcessamento>(db),
            bling,
            tokens,
            opcoes,
            ContasAntigasEmAberto(db, bling, NullLogger<ContasAntigasEmAberto>.Instance),
            FornecedoresBlingService(db, bling, NullLogger<FornecedoresBlingService>.Instance),
            ConferenciaBlingService(db, bling),
            new ContasReceberBlingService(
                new Repositorio<ContaReceberSincronizada>(db),
                bling,
                FornecedoresBlingService(db, bling, NullLogger<FornecedoresBlingService>.Instance),
                NullLogger<ContasReceberBlingService>.Instance),
            new CategoriasBlingService(new Repositorio<CategoriaFinanceira>(db), bling, NullLogger<CategoriasBlingService>.Instance),
            NullLogger<SincronizacaoContasService>.Instance);
    }

    public static ContasAntigasEmAberto ContasAntigasEmAberto(
        PrevisioDbContext db,
        BlingClient bling,
        ILogger<ContasAntigasEmAberto> logger)
    {
        return new ContasAntigasEmAberto(
            new Repositorio<ContaPagarSincronizada>(db),
            bling,
            logger);
    }

    public static FornecedoresBlingService FornecedoresBlingService(
        PrevisioDbContext db,
        BlingClient bling,
        ILogger<FornecedoresBlingService> logger)
    {
        return new FornecedoresBlingService(
            new Repositorio<ParcelaNota>(db),
            new Repositorio<FornecedorBling>(db),
            bling,
            logger);
    }

    public static TokenBlingService TokenBlingService(
        BlingClient bling,
        PrevisioDbContext db,
        IProtetorSegredos protetor,
        ILogger<TokenBlingService> logger)
    {
        return new TokenBlingService(
            bling,
            new Repositorio<Cliente>(db),
            protetor,
            logger);
    }

    public static SaudeService SaudeService(
        PrevisioDbContext db,
        AvisosService avisos,
        IAgendaJobs agenda,
        TimeProvider relogio)
    {
        return new SaudeService(
            new Repositorio<Cliente>(db),
            new Repositorio<Aviso>(db),
            new Repositorio<NotaFiscal>(db),
            avisos,
            agenda,
            relogio);
    }

    public static NotasSoPdfService NotasSoPdfService(
        PrevisioDbContext db,
        RegistroNotaService registro,
        LeitorNFe leitor,
        IEnvioEmail email,
        TimeProvider relogio,
        ILogger<NotasSoPdfService> logger)
    {
        return new NotasSoPdfService(
            new Repositorio<NotaSoPdf>(db),
            new Repositorio<NotaFiscal>(db),
            new Repositorio<Cliente>(db),
            new Repositorio<Empresa>(db),
            registro,
            leitor,
            email,
            relogio,
            logger);
    }

    public static BuscaXmlSefazService BuscaXmlSefazService(
        PrevisioDbContext db,
        ISefaz sefaz,
        CertificadosService certificados,
        RegistroNotaService registro,
        LeitorNFe leitor,
        TimeProvider relogio,
        ILogger<BuscaXmlSefazService> logger)
    {
        return new BuscaXmlSefazService(
            new Repositorio<Cliente>(db),
            new Repositorio<NotaSoPdf>(db),
            new Repositorio<NotaFiscal>(db),
            new Repositorio<Empresa>(db),
            sefaz,
            certificados,
            registro,
            leitor,
            relogio,
            logger);
    }

    public static AvisosService AvisosService(
        PrevisioDbContext db,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<AvisosService> logger)
    {
        return new AvisosService(
            new Repositorio<Cliente>(db),
            new Repositorio<Aviso>(db),
            new Repositorio<Usuario>(db),
            new Repositorio<NotaFiscal>(db),
            new Repositorio<NotaSoPdf>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            email,
            configuracao,
            relogio,
            logger);
    }

    public static ContratosService ContratosService(PrevisioDbContext db, TimeProvider relogio)
    {
        return new ContratosService(
            new Repositorio<Cliente>(db),
            relogio);
    }

    public static AvisosTesteService AvisosTesteService(
        PrevisioDbContext db,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<AvisosTesteService> logger)
    {
        return new AvisosTesteService(
            new Repositorio<Cliente>(db),
            new Repositorio<Usuario>(db),
            email,
            configuracao,
            relogio,
            logger);
    }

    public static ResumoSemanalService ResumoSemanalService(
        PrevisioDbContext db,
        AvisosService avisos,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<ResumoSemanalService> logger)
    {
        return new ResumoSemanalService(
            new Repositorio<Cliente>(db),
            new Repositorio<Usuario>(db),
            new Repositorio<Empresa>(db),
            new Repositorio<ContaPagarSincronizada>(db),
            new Repositorio<NotaFiscal>(db),
            new Repositorio<Aviso>(db),
            avisos,
            email,
            configuracao,
            relogio,
            logger);
    }

    public static RegistroNotaService RegistroNotaService(PrevisioDbContext db)
    {
        return new RegistroNotaService(
            new Repositorio<NotaFiscal>(db),
            new Repositorio<Empresa>(db));
    }

    public static RegistroNotaSoPdf RegistroNotaSoPdf(PrevisioDbContext db)
    {
        return new RegistroNotaSoPdf(
            new Repositorio<NotaSoPdf>(db),
            new Repositorio<NotaFiscal>(db));
    }

    public static EmailsIgnoradosService EmailsIgnoradosService(
        PrevisioDbContext db,
        RegistroNotaSoPdf notasSoPdf,
        TimeProvider relogio,
        IOptions<LeituraEmailOptions> opcoes)
    {
        return new EmailsIgnoradosService(
            new Repositorio<EmailIgnorado>(db),
            notasSoPdf,
            relogio,
            opcoes);
    }
}
