using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Contratos;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;

namespace Previsio.Api.Tests.Contratos;

public class TesteGratisTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly EnvioEmailDeTeste _email = new EnvioEmailDeTeste();
    // Quarta, 11/11/2026, 09h em Brasília
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();

    private DateTime Agora
    {
        get { return _relogio.Agora.UtcDateTime; }
    }

    private Cliente Rede(string nome, SituacaoContrato contrato, DateTime? terminaEm)
    {
        var rede = new Cliente();
        rede.Nome = nome;
        rede.Contrato = contrato;
        rede.TesteTerminaEm = terminaEm;
        _db.Clientes.Add(rede);
        return rede;
    }

    private void Usuario(string email, PerfilUsuario perfil, int? clienteId)
    {
        var usuario = new Usuario();
        usuario.Nome = email.Split('@')[0];
        usuario.Email = email;
        usuario.SenhaHash = "x";
        usuario.Perfil = perfil;
        usuario.ClienteId = clienteId;
        _db.Usuarios.Add(usuario);
    }

    private AvisosTesteService Avisos()
    {
        IConfiguration configuracao = new ConfigurationBuilder().Build();
        return new AvisosTesteService(_db, _email, configuracao, _relogio, NullLogger<AvisosTesteService>.Instance);
    }

    [Fact]
    public void Quinze_dias_contando_hoje_terminam_a_meia_noite_de_brasilia()
    {
        DateTime termino = PeriodoTeste.Termino(new DateTime(2026, 11, 11), PeriodoTeste.DiasPadrao);

        // 26/11 00h em Brasília = 03h UTC; o último dia é 25/11
        Assert.Equal(new DateTime(2026, 11, 26, 3, 0, 0), termino);
        Assert.Equal(new DateTime(2026, 11, 25), PeriodoTeste.UltimoDia(termino));
        Assert.Equal(15, PeriodoTeste.DiasRestantes(termino, Agora));
        Assert.Equal(1, PeriodoTeste.DiasRestantes(termino, new DateTime(2026, 11, 26, 2, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(0, PeriodoTeste.DiasRestantes(termino, new DateTime(2026, 11, 26, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Jobs_processam_contratadas_e_testes_no_prazo_e_param_os_vencidos_e_desativados()
    {
        Rede("Contratada", SituacaoContrato.Contratado, null);
        Rede("No prazo", SituacaoContrato.EmTeste, Agora.AddDays(2));
        Rede("Vencido", SituacaoContrato.EmTeste, Agora.AddMinutes(-1));
        Rede("Sem data", SituacaoContrato.EmTeste, null);
        Cliente desativada = Rede("Desativada", SituacaoContrato.Contratado, null);
        desativada.Ativo = false;
        await _db.SaveChangesAsync();

        List<string> processadas = _db.Clientes.Where(PeriodoTeste.Processa(Agora)).Select(c => c.Nome).OrderBy(n => n).ToList();

        Assert.Equal(new List<string> { "Contratada", "No prazo" }, processadas);
    }

    [Fact]
    public async Task Cadastro_em_teste_comeca_com_15_dias_e_a_tela_recebe_os_dias()
    {
        var protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());
        ClientesService clientes = ServicosDeTeste.Clientes(_db, protetor);

        Resultado<ClienteDto> emTeste = await clientes.CriarAsync(new NovoClienteDto { Nome = "Rede nova", EmTeste = true }, CancellationToken.None);
        Resultado<ClienteDto> contratada = await clientes.CriarAsync(new NovoClienteDto { Nome = "Rede antiga" }, CancellationToken.None);

        Assert.Equal("EmTeste", emTeste.Valor!.Contrato);
        Assert.Equal(15, emTeste.Valor.DiasRestantesTeste);
        Assert.Equal(RelogioBrasil.Hoje().AddDays(14), emTeste.Valor.TesteUltimoDia);
        Assert.Equal("Contratado", contratada.Valor!.Contrato);
        Assert.Null(contratada.Valor.DiasRestantesTeste);
    }

    [Fact]
    public async Task Contratar_tira_do_teste_e_prorrogar_soma_do_fim_ou_de_hoje()
    {
        Cliente noPrazo = Rede("No prazo", SituacaoContrato.EmTeste, PeriodoTeste.Termino(new DateTime(2026, 11, 11), 5));
        Cliente vencido = Rede("Vencido", SituacaoContrato.EmTeste, Agora.AddDays(-3));
        vencido.TesteAvisoEncerradoEnviadoEm = Agora.AddDays(-3);
        await _db.SaveChangesAsync();
        var contratos = new ContratosService(_db, _relogio);

        // No prazo (último dia 15/11): +7 dias vai até 22/11
        Resultado<ClienteDto> prorrogado = await contratos.ProrrogarAsync(noPrazo.Id, 7, CancellationToken.None);
        Assert.Equal(new DateTime(2026, 11, 22), prorrogado.Valor!.TesteUltimoDia);

        // Já vencido: +7 dias a partir de hoje (11/11 a 17/11) e os avisos valem de novo
        Resultado<ClienteDto> reaberto = await contratos.ProrrogarAsync(vencido.Id, 7, CancellationToken.None);
        Assert.Equal("EmTeste", reaberto.Valor!.Contrato);
        Assert.Equal(new DateTime(2026, 11, 17), reaberto.Valor.TesteUltimoDia);
        Assert.Null(_db.Clientes.Single(c => c.Id == vencido.Id).TesteAvisoEncerradoEnviadoEm);

        Assert.Equal(TipoResultado.Invalido, (await contratos.ProrrogarAsync(noPrazo.Id, 0, CancellationToken.None)).Tipo);

        Resultado<ClienteDto> contratado = await contratos.ContratarAsync(vencido.Id, CancellationToken.None);
        Assert.Equal("Contratado", contratado.Valor!.Contrato);
        Assert.NotNull(_db.Clientes.Single(c => c.Id == vencido.Id).ContratadoEm);
        Assert.Equal(TipoResultado.NaoEncontrado, (await contratos.ContratarAsync(999, CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task Avisa_3_dias_antes_e_no_fim_uma_vez_so_para_a_rede_e_os_admins()
    {
        // Último dia: 13/11 (faltam 3 dias contando hoje, 11/11)
        Cliente rede = Rede("Rede Auto Center", SituacaoContrato.EmTeste, PeriodoTeste.Termino(new DateTime(2026, 11, 11), 3));
        Cliente outra = Rede("Outra rede", SituacaoContrato.EmTeste, PeriodoTeste.Termino(new DateTime(2026, 11, 11), 10));
        await _db.SaveChangesAsync();
        Usuario("ana@autocenter.com.br", PerfilUsuario.Cliente, rede.Id);
        Usuario("joao@outra.com.br", PerfilUsuario.Cliente, outra.Id);
        Usuario("admin@softio.com.br", PerfilUsuario.Admin, null);
        await _db.SaveChangesAsync();

        await Avisos().EnviarAsync(CancellationToken.None);
        await Avisos().EnviarAsync(CancellationToken.None);

        Assert.Equal(2, _email.Enviados.Count);
        Assert.Equal("Previsio: faltam 3 dias de teste grátis", _email.Enviados.Single(e => e.Para == "ana@autocenter.com.br").Assunto);
        Assert.Equal("Previsio: teste da Rede Auto Center vai até 13/11/2026", _email.Enviados.Single(e => e.Para == "admin@softio.com.br").Assunto);

        // Acabou o prazo: aviso de encerrado, também uma vez só
        _relogio.Agora = new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        await Avisos().EnviarAsync(CancellationToken.None);
        await Avisos().EnviarAsync(CancellationToken.None);

        Assert.Equal(4, _email.Enviados.Count);
        Assert.Equal("Previsio: seu teste grátis acabou", _email.Enviados[2].Assunto);
        Assert.Contains("acabou em 13/11/2026", _email.Enviados[2].Texto);
        Assert.Equal("Previsio: teste da Rede Auto Center acabou", _email.Enviados[3].Assunto);
        Assert.DoesNotContain(_email.Enviados, e => e.Para == "joao@outra.com.br");
    }
}
