using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Autenticacao;
using Previsio.Api.Sefaz;
using Previsio.Api.Servicos;
using Previsio.Bling;
using Previsio.Core.Entidades;
using Previsio.Core.Painel;
using Previsio.Data;
using Previsio.Data.Repositorios;

namespace Previsio.Api.Tests.Apoio;

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
        return new CertificadosService(db, protetor, TimeProvider.System);
    }

    public static AutenticacaoService Autenticacao(PrevisioDbContext db)
    {
        return new AutenticacaoService(db, new PasswordHasher<Usuario>(), NullLogger<AutenticacaoService>.Instance);
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
            new CalculadoraPainel());
    }

    public static NotasService Notas(PrevisioDbContext db, IBackgroundJobClient fila)
    {
        return new NotasService(new Repositorio<NotaFiscal>(db), fila);
    }

    public static FalhasService Falhas(PrevisioDbContext db)
    {
        return new FalhasService(new Repositorio<FalhaProcessamento>(db));
    }

    public static BlingConexaoService ConexaoBling(
        PrevisioDbContext db, ProtetorSegredos protetor, BlingClient bling, IBackgroundJobClient fila)
    {
        var tokens = new TokenBlingService(bling, db, protetor, NullLogger<TokenBlingService>.Instance);
        return new BlingConexaoService(
            new Repositorio<Empresa>(db),
            Acesso(db),
            bling,
            tokens,
            fila,
            new EphemeralDataProtectionProvider(),
            NullLogger<BlingConexaoService>.Instance);
    }
}
