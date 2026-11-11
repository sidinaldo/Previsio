using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;
using Previsio.Api.Email;
using Previsio.Api.Resumos;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;

namespace Previsio.Api.Tests.Servicos;

public class ResumoSemanalServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly EnvioEmailDeTeste _email = new EnvioEmailDeTeste();
    // Segunda-feira, 16/11/2026, 10h em Brasília (13h UTC)
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste { Agora = new DateTimeOffset(2026, 11, 16, 13, 0, 0, TimeSpan.Zero) };

    private Cliente _rede = null!;
    private Empresa _centro = null!;
    private Empresa _zonaNorte = null!;
    private Usuario _ana = null!;

    private ResumoSemanalService CriarServico()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Front:Url", "https://app.previsio.com.br/#" } })
            .Build();
        var avisos = new AvisosService(_db, _email, configuracao, _relogio, NullLogger<AvisosService>.Instance);
        return new ResumoSemanalService(_db, avisos, _email, configuracao, _relogio, NullLogger<ResumoSemanalService>.Instance);
    }

    private async Task CadastrarRede()
    {
        _rede = new Cliente();
        _rede.Nome = "Rede Auto Center";
        _db.Clientes.Add(_rede);
        var outraRede = new Cliente();
        outraRede.Nome = "Outra rede";
        // A outra rede recebe na sexta: os usuários dela não podem receber o resumo desta
        outraRede.ResumoSemanalDia = (int)DayOfWeek.Friday;
        _db.Clientes.Add(outraRede);
        await _db.SaveChangesAsync();

        _centro = Empresa("Auto Center Centro", "11222333000262");
        _zonaNorte = Empresa("Auto Center Zona Norte", "11222333000181");

        _ana = Usuario("ana@autocenter.com.br", PerfilUsuario.Cliente, _rede.Id);
        Usuario("bruno@autocenter.com.br", PerfilUsuario.Cliente, _rede.Id);
        Usuario("admin@softio.com.br", PerfilUsuario.Admin, null);
        Usuario("joao@outra.com.br", PerfilUsuario.Cliente, outraRede.Id);
        Usuario inativo = Usuario("saiu@autocenter.com.br", PerfilUsuario.Cliente, _rede.Id);
        inativo.Ativo = false;
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

    private Usuario Usuario(string email, PerfilUsuario perfil, int? clienteId)
    {
        var usuario = new Usuario();
        usuario.Nome = email.Split('@')[0];
        usuario.Email = email;
        usuario.SenhaHash = "x";
        usuario.Perfil = perfil;
        usuario.ClienteId = clienteId;
        _db.Usuarios.Add(usuario);
        return usuario;
    }

    private void Conta(Empresa empresa, int diasAFrente, decimal valor, int situacao = SituacaoContaBling.EmAberto)
    {
        var conta = new ContaPagarSincronizada();
        conta.EmpresaId = empresa.Id;
        conta.BlingId = 1000 + _db.ChangeTracker.Entries<ContaPagarSincronizada>().Count();
        conta.Vencimento = new DateTime(2026, 11, 16).AddDays(diasAFrente);
        conta.Valor = valor;
        conta.Situacao = situacao;
        _db.ContasPagar.Add(conta);
    }

    private void Nota(string numero, StatusNota status, DateTime recebidaEmUtc, decimal valor)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = _rede.Id;
        nota.EmpresaId = _centro.Id;
        nota.Numero = numero;
        nota.ChaveAcesso = "chave-" + numero;
        nota.NomeFornecedor = "Alfa Distribuidora";
        nota.Status = status;
        nota.ValorTotal = valor;
        nota.RecebidaEm = recebidaEmUtc;
        _db.NotasFiscais.Add(nota);
    }

    [Fact]
    public async Task Rede_nova_recebe_na_segunda_as_7h()
    {
        var cliente = new Cliente();
        Assert.True(cliente.ResumoSemanalAtivo);
        Assert.Equal((int)DayOfWeek.Monday, cliente.ResumoSemanalDia);
        Assert.Equal(7, cliente.ResumoSemanalHora);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Envia_no_dia_e_hora_da_rede_so_para_os_usuarios_ativos_dela()
    {
        await CadastrarRede();

        await CriarServico().EnviarDevidosAsync(CancellationToken.None);

        List<string> para = _email.Enviados.Select(e => e.Para).OrderBy(p => p).ToList();
        Assert.Equal(new List<string> { "ana@autocenter.com.br", "bruno@autocenter.com.br" }, para);
        Cliente rede = _db.Clientes.Single(c => c.Id == _rede.Id);
        Assert.Equal(_relogio.Agora.UtcDateTime, rede.ResumoSemanalEnviadoEm);
    }

    [Fact]
    public async Task Nao_envia_duas_vezes_no_mesmo_dia()
    {
        await CadastrarRede();
        ResumoSemanalService servico = CriarServico();

        await servico.EnviarDevidosAsync(CancellationToken.None);
        _relogio.Agora = _relogio.Agora.AddHours(1);
        await servico.EnviarDevidosAsync(CancellationToken.None);

        Assert.Equal(2, _email.Enviados.Count);
    }

    [Fact]
    public async Task Volta_a_enviar_na_semana_seguinte()
    {
        await CadastrarRede();
        ResumoSemanalService servico = CriarServico();

        await servico.EnviarDevidosAsync(CancellationToken.None);
        _relogio.Agora = _relogio.Agora.AddDays(7);
        await servico.EnviarDevidosAsync(CancellationToken.None);

        Assert.Equal(4, _email.Enviados.Count);
    }

    [Fact]
    public async Task Nao_envia_antes_da_hora_nem_em_outro_dia_nem_desligado()
    {
        await CadastrarRede();
        ResumoSemanalService servico = CriarServico();

        // Segunda, 6h em Brasília: ainda não chegou a hora
        _relogio.Agora = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
        await servico.EnviarDevidosAsync(CancellationToken.None);
        // Terça: não é o dia
        _relogio.Agora = new DateTimeOffset(2026, 11, 17, 13, 0, 0, TimeSpan.Zero);
        await servico.EnviarDevidosAsync(CancellationToken.None);
        Assert.Empty(_email.Enviados);

        // Desligado: nem na segunda seguinte
        Cliente rede = _db.Clientes.Single(c => c.Id == _rede.Id);
        rede.ResumoSemanalAtivo = false;
        await _db.SaveChangesAsync();
        _relogio.Agora = new DateTimeOffset(2026, 11, 23, 13, 0, 0, TimeSpan.Zero);
        await servico.EnviarDevidosAsync(CancellationToken.None);
        Assert.Empty(_email.Enviados);
    }

    [Fact]
    public async Task Servidor_de_email_fora_do_ar_nao_derruba_o_job()
    {
        await CadastrarRede();
        _email.Falhar = true;

        await CriarServico().EnviarDevidosAsync(CancellationToken.None);

        Assert.Empty(_email.Enviados);
        Assert.NotNull(_db.Clientes.Single(c => c.Id == _rede.Id).ResumoSemanalEnviadoEm);
    }

    [Fact]
    public async Task Resumo_soma_os_vencimentos_em_aberto_por_dia_e_empresa_e_as_notas_da_semana()
    {
        await CadastrarRede();
        Conta(_centro, 0, 1000.00m);
        Conta(_zonaNorte, 0, 840.50m);
        Conta(_zonaNorte, 0, 100.00m);
        Conta(_centro, 3, 500.00m, SituacaoContaBling.ParcialmentePago);
        Conta(_centro, 4, 999.00m, SituacaoContaBling.Pago);
        Conta(_centro, 6, 200.00m);
        Conta(_centro, 7, 300.00m);
        Conta(_centro, 30, 50.00m);
        Conta(_centro, 31, 9999.00m);
        Conta(_centro, -1, 7777.00m);
        // Últimos 7 dias em Brasília: de 09/11 00h até 15/11 23h59
        Nota("1", StatusNota.Lancada, new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc), 1000m);
        Nota("2", StatusNota.LancadaManualmente, new DateTime(2026, 11, 15, 20, 0, 0, DateTimeKind.Utc), 200m);
        Nota("3", StatusNota.SemParcelas, new DateTime(2026, 11, 9, 4, 0, 0, DateTimeKind.Utc), 300m);
        Nota("4", StatusNota.Lancada, new DateTime(2026, 11, 9, 2, 0, 0, DateTimeKind.Utc), 5000m);
        Nota("5", StatusNota.Recebida, new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc), 7000m);
        await _db.SaveChangesAsync();

        ResumoSemanal resumo = await CriarServico().MontarAsync(_rede, CancellationToken.None);

        Assert.Equal(new DateTime(2026, 11, 16), resumo.Hoje);
        Assert.Equal(2640.50m, resumo.Total7Dias);
        Assert.Equal(2990.50m, resumo.Total30Dias);
        Assert.Equal(7, resumo.Contas30Dias);
        Assert.Equal(3, resumo.Dias.Count);
        VencimentoDoDia hoje = resumo.Dias[0];
        Assert.Equal(1940.50m, hoje.Total);
        Assert.Equal("Auto Center Centro", hoje.Empresas[0].Empresa);
        Assert.Equal("Auto Center Zona Norte", hoje.Empresas[1].Empresa);
        Assert.Equal(2, hoje.Empresas[1].Quantidade);
        Assert.Equal(940.50m, hoje.Empresas[1].Valor);

        Assert.Equal(3, resumo.NotasRecebidas);
        Assert.Equal(1500m, resumo.ValorNotasRecebidas);
        Assert.Equal(2, resumo.NotasLancadas);
        Assert.Equal(1, resumo.NotasComProblema);
        // A nota sem parcelas vira aviso (as empresas sem Bling também)
        Assert.Contains(resumo.Problemas, a => a.Tipo == TipoAviso.NotaComProblema);
    }

    [Fact]
    public async Task Email_traz_valores_em_reais_dia_da_semana_e_link_do_painel()
    {
        await CadastrarRede();
        Conta(_centro, 1, 1840.50m);
        await _db.SaveChangesAsync();

        await CriarServico().EnviarDevidosAsync(CancellationToken.None);

        MensagemEmail email = _email.Enviados.First(e => e.Para == "ana@autocenter.com.br");
        Assert.Equal("Previsio: R$ 1.840,50 a pagar nos próximos 7 dias · Rede Auto Center", email.Assunto);
        Assert.Contains("ter, 17/11", email.Html);
        Assert.Contains("Auto Center Centro · 1 conta: R$ 1.840,50", email.Texto);
        Assert.Contains("https://app.previsio.com.br/#/pages/painel", email.Html);
        Assert.Contains("R$ 1.840,50", email.Texto);
        Assert.Contains("Nenhuma nota chegou pelo e-mail.", email.Texto);
    }

    [Fact]
    public async Task Alterar_valida_dia_e_hora_e_grava()
    {
        await CadastrarRede();
        ResumoSemanalService servico = CriarServico();

        var dados = new AlteracaoResumoSemanalDto { Ativo = true, Dia = 7, Hora = 8 };
        Assert.Equal(TipoResultado.Invalido, (await servico.AlterarAsync(_rede.Id, dados, CancellationToken.None)).Tipo);
        dados = new AlteracaoResumoSemanalDto { Ativo = true, Dia = 5, Hora = 24 };
        Assert.Equal(TipoResultado.Invalido, (await servico.AlterarAsync(_rede.Id, dados, CancellationToken.None)).Tipo);

        dados = new AlteracaoResumoSemanalDto { Ativo = false, Dia = 5, Hora = 16 };
        Resultado<ResumoSemanalDto> resultado = await servico.AlterarAsync(_rede.Id, dados, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor!.Ativo);
        Assert.Equal(5, resultado.Valor.Dia);
        Assert.Equal(16, resultado.Valor.Hora);
        Assert.Equal(2, resultado.Valor.Destinatarios);
        Assert.Equal(TipoResultado.NaoEncontrado, (await servico.AlterarAsync(999, dados, CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task Teste_vai_so_para_quem_pediu_e_nao_conta_como_envio_da_semana()
    {
        await CadastrarRede();
        ResumoSemanalService servico = CriarServico();

        Resultado resultado = await servico.EnviarTesteAsync(_rede.Id, _ana.Id, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Single(_email.Enviados);
        Assert.Equal("ana@autocenter.com.br", _email.Enviados[0].Para);
        Assert.Null(_db.Clientes.Single(c => c.Id == _rede.Id).ResumoSemanalEnviadoEm);
    }

    [Fact]
    public async Task Teste_sem_smtp_ou_com_falha_explica_o_motivo()
    {
        await CadastrarRede();
        ResumoSemanalService servico = CriarServico();

        _email.Configurado = false;
        Resultado semSmtp = await servico.EnviarTesteAsync(_rede.Id, _ana.Id, CancellationToken.None);
        Assert.Equal(ResumoSemanalService.MensagemSmtpNaoConfigurado, semSmtp.Mensagem);

        _email.Configurado = true;
        _email.Falhar = true;
        Resultado comFalha = await servico.EnviarTesteAsync(_rede.Id, _ana.Id, CancellationToken.None);
        Assert.Equal(ResumoSemanalService.MensagemFalhaEnvio, comFalha.Mensagem);
    }
}
