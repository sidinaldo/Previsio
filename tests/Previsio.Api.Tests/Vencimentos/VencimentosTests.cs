using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Avisos;
using Previsio.Aplicacao.Resumos;
using Previsio.Aplicacao.Vencimentos;
using Previsio.Aplicacao.Dtos;

namespace Previsio.Api.Tests.Vencimentos;

public class VencimentosTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly EnvioEmailDeTeste _email = new EnvioEmailDeTeste();
    // Quarta, 11/11/2026, 09h em Brasília
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private readonly IConfiguration _configuracao = new ConfigurationBuilder().Build();

    private Cliente _rede = null!;
    private Empresa _norte = null!;
    private Empresa _centro = null!;
    private int _blingId = 1;

    private async Task CadastrarRede()
    {
        _rede = new Cliente();
        _rede.Nome = "Rede Auto Center";
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();
        _norte = Empresa("Auto Center Zona Norte");
        _centro = Empresa("Auto Center Centro");
        var usuario = new Usuario();
        usuario.Nome = "Ana";
        usuario.Email = "ana@autocenter.com.br";
        usuario.SenhaHash = "x";
        usuario.Perfil = PerfilUsuario.Cliente;
        usuario.ClienteId = _rede.Id;
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();
    }

    private Empresa Empresa(string nome)
    {
        var empresa = new Empresa();
        empresa.ClienteId = _rede.Id;
        empresa.Nome = nome;
        empresa.Cnpj = "1122233300" + _db.ChangeTracker.Entries<Empresa>().Count().ToString("0000");
        // Conectada ao Bling: não gera o aviso de desconectada
        empresa.BlingRefreshTokenProtegido = "x";
        _db.Empresas.Add(empresa);
        return empresa;
    }

    private void Conta(Empresa empresa, int dias, decimal valor, int situacao = SituacaoContaBling.EmAberto)
    {
        var conta = new ContaPagarSincronizada();
        conta.EmpresaId = empresa.Id;
        conta.BlingId = _blingId++;
        conta.Vencimento = new DateTime(2026, 11, 11).AddDays(dias);
        conta.Valor = valor;
        conta.Situacao = situacao;
        _db.ContasPagar.Add(conta);
    }

    private AvisosService Avisos()
    {
        return ServicosDeTeste.AvisosService(_db, _email, _configuracao, _relogio, NullLogger<AvisosService>.Instance);
    }

    private AlertaVencimentosService Alertas()
    {
        return ServicosDeTeste.AlertaVencimentosService(_db, _email, _configuracao, _relogio, NullLogger<AlertaVencimentosService>.Instance);
    }

    [Fact]
    public async Task Vencidas_viram_um_aviso_por_loja_depois_da_tolerancia_e_somem_quando_pagas()
    {
        await CadastrarRede();
        Conta(_norte, -1, 1000m);
        Conta(_norte, -5, 320m, SituacaoContaBling.ParcialmentePago);
        Conta(_norte, -3, 999m, SituacaoContaBling.Pago);
        Conta(_centro, 0, 50m);
        await _db.SaveChangesAsync();

        await Avisos().AtualizarAsync(_rede.Id, CancellationToken.None);

        Aviso aviso = _db.Avisos.Single();
        Assert.Equal(TipoAviso.ContasVencidas, aviso.Tipo);
        Assert.Equal("vencidas:" + _norte.Id, aviso.Chave);
        Assert.Equal("2 contas vencidas: Auto Center Zona Norte", aviso.Titulo);
        Assert.Equal("R$ 1.320,00 em aberto no Bling; a mais antiga venceu em 06/11/2026. Pague ou dê baixa no Bling.", aviso.Mensagem);

        // Tolerância de 3 dias: a de ontem ainda não conta; o texto do mesmo aviso é atualizado
        Cliente rede = _db.Clientes.Single(c => c.Id == _rede.Id);
        rede.AvisoVencidasDias = 3;
        await _db.SaveChangesAsync();
        await Avisos().AtualizarAsync(_rede.Id, CancellationToken.None);
        Aviso atualizado = _db.Avisos.Single();
        Assert.Null(atualizado.ResolvidoEm);
        Assert.Equal("1 conta vencida: Auto Center Zona Norte", atualizado.Titulo);

        // Pagas no Bling: o aviso fecha sozinho
        foreach (ContaPagarSincronizada conta in _db.ContasPagar.Where(c => c.EmpresaId == _norte.Id))
        {
            conta.Situacao = SituacaoContaBling.Pago;
        }
        await _db.SaveChangesAsync();
        await Avisos().AtualizarAsync(_rede.Id, CancellationToken.None);
        Assert.NotNull(_db.Avisos.Single().ResolvidoEm);
    }

    [Fact]
    public async Task Alerta_diario_traz_hoje_amanha_e_vencidas_e_sai_uma_vez_por_dia_na_hora()
    {
        await CadastrarRede();
        Conta(_norte, 0, 1000m);
        Conta(_centro, 0, 840.50m);
        Conta(_centro, 1, 200m);
        Conta(_norte, -2, 99m);
        Conta(_norte, 2, 5000m);
        await _db.SaveChangesAsync();
        Cliente rede = _db.Clientes.Single(c => c.Id == _rede.Id);
        rede.AlertaVencimentosHora = 8;
        await _db.SaveChangesAsync();

        await Alertas().EnviarDevidosAsync(CancellationToken.None);
        await Alertas().EnviarDevidosAsync(CancellationToken.None);

        MensagemEmail email = Assert.Single(_email.Enviados);
        Assert.Equal("ana@autocenter.com.br", email.Para);
        Assert.Equal("Previsio: hoje vencem R$ 1.840,50 em 2 contas · Rede Auto Center", email.Assunto);
        Assert.Contains("Olá, Ana.", email.Texto);
        Assert.Contains("VENCEM HOJE: R$ 1.840,50", email.Texto);
        Assert.Contains("Auto Center Centro · 1 conta · R$ 840,50", email.Texto);
        Assert.Contains("VENCEM AMANHÃ: R$ 200,00", email.Texto);
        Assert.Contains("1 conta vencida em aberto no Bling, somando R$ 99,00", email.Texto);
        Assert.DoesNotContain("5.000", email.Texto);

        // No dia seguinte, antes da hora escolhida, não manda
        _relogio.Agora = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        await Alertas().EnviarDevidosAsync(CancellationToken.None);
        Assert.Single(_email.Enviados);
    }

    [Fact]
    public async Task Sem_nada_vencendo_nao_manda_e_desligado_nao_manda()
    {
        await CadastrarRede();
        Conta(_norte, 5, 100m);
        await _db.SaveChangesAsync();

        await Alertas().EnviarDevidosAsync(CancellationToken.None);
        Assert.Empty(_email.Enviados);

        Conta(_norte, 0, 100m);
        Cliente rede = _db.Clientes.Single(c => c.Id == _rede.Id);
        rede.AlertaVencimentosAtivo = false;
        rede.AlertaVencimentosEnviadoEm = null;
        await _db.SaveChangesAsync();
        await Alertas().EnviarDevidosAsync(CancellationToken.None);
        Assert.Empty(_email.Enviados);
    }

    [Fact]
    public async Task Configuracao_valida_e_grava()
    {
        await CadastrarRede();

        Resultado<AlertasVencimentoDto> zero = await Alertas().AlterarAsync(_rede.Id,
            new AlertasVencimentoDto { AvisoVencidasDias = 0, AlertaDiarioAtivo = true, AlertaDiarioHora = 7 }, CancellationToken.None);
        Assert.Equal(TipoResultado.Invalido, zero.Tipo);

        Resultado<AlertasVencimentoDto> ok = await Alertas().AlterarAsync(_rede.Id,
            new AlertasVencimentoDto { AvisoVencidasDias = 5, AlertaDiarioAtivo = false, AlertaDiarioHora = 6 }, CancellationToken.None);
        Assert.True(ok.Sucesso);
        Assert.Equal(5, (await Alertas().ObterAsync(_rede.Id, CancellationToken.None)).Valor!.AvisoVencidasDias);
        Assert.False(_db.Clientes.Single(c => c.Id == _rede.Id).AlertaVencimentosAtivo);
    }

    [Fact]
    public async Task Resumo_e_painel_mostram_as_vencidas()
    {
        await CadastrarRede();
        Conta(_norte, -1, 100m);
        Conta(_centro, -10, 50.50m);
        await _db.SaveChangesAsync();

        var resumoService = ServicosDeTeste.ResumoSemanalService(_db, Avisos(), _email, _configuracao, _relogio, NullLogger<ResumoSemanalService>.Instance);
        ResumoSemanal resumo = await resumoService.MontarAsync(_db.Clientes.Single(c => c.Id == _rede.Id), CancellationToken.None);
        Assert.Equal(2, resumo.Vencidas);
        Assert.Equal(150.50m, resumo.TotalVencidas);
        Assert.Contains("2 contas vencidas em aberto no Bling, somando R$ 150,50", ModeloEmailResumo.Montar(resumo, _db.Usuarios.First(), "https://x/#").Texto);
    }
}
