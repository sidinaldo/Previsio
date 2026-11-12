using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;
using Previsio.Api.Saude;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Infra.Seguranca;

namespace Previsio.Api.Tests.Servicos;

public class SaudeServiceTests
{
    private class AgendaDeTeste : IAgendaJobs
    {
        public List<ExecucaoAgendada> Lista { get; } = new List<ExecucaoAgendada>();

        public List<ExecucaoAgendada> Agendados()
        {
            return Lista;
        }
    }

    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private readonly AgendaDeTeste _agenda = new AgendaDeTeste();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private SaudeService CriarServico()
    {
        IConfiguration configuracao = new ConfigurationBuilder().Build();
        var avisos = ServicosDeTeste.AvisosService(_db, new EnvioEmailDeTeste(), configuracao, _relogio, NullLogger<AvisosService>.Instance);
        return ServicosDeTeste.SaudeService(_db, avisos, _agenda, _relogio);
    }

    private Cliente Rede(string nome)
    {
        var cliente = new Cliente();
        cliente.Nome = nome;
        cliente.ImapHost = "imap.gmail.com";
        cliente.ImapUsuario = "notas@" + nome.Replace(" ", "").ToLowerInvariant() + ".com.br";
        _db.Clientes.Add(cliente);
        return cliente;
    }

    private Empresa Empresa(Cliente rede, string nome, bool conectada)
    {
        var empresa = new Empresa();
        empresa.Cliente = rede;
        empresa.Nome = nome;
        empresa.Cnpj = "11222333000181";
        if (conectada)
        {
            empresa.BlingAccessTokenProtegido = _protetor.Proteger("token");
            empresa.BlingRefreshTokenProtegido = _protetor.Proteger("refresh");
            empresa.BlingTokenExpiraEm = new DateTime(2026, 11, 11, 18, 0, 0, DateTimeKind.Utc);
            empresa.UltimaSincronizacaoEm = new DateTime(2026, 11, 11, 11, 30, 0, DateTimeKind.Utc);
        }
        _db.Empresas.Add(empresa);
        return empresa;
    }

    private void Nota(Cliente rede, Empresa? empresa, StatusNota status, DateTime recebidaEm)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = rede.Id;
        nota.EmpresaId = empresa == null ? null : empresa.Id;
        nota.Numero = Guid.NewGuid().ToString("N").Substring(0, 6);
        nota.ChaveAcesso = nota.Numero;
        nota.NomeFornecedor = "Alfa";
        nota.Status = status;
        nota.RecebidaEm = recebidaEm;
        _db.NotasFiscais.Add(nota);
    }

    [Fact]
    public async Task Rede_com_aviso_aberto_pede_atencao_e_vem_primeiro()
    {
        Cliente emDia = Rede("A Rede em dia");
        Empresa(emDia, "Loja 1", true);
        Cliente comProblema = Rede("Z Rede com problema");
        Empresa(comProblema, "Loja Centro", true);
        Empresa(comProblema, "Loja Sul", false);
        await _db.SaveChangesAsync();

        SaudeDto saude = await CriarServico().ObterAsync(CancellationToken.None);

        Assert.Equal(1, saude.RedesComAtencao);
        Assert.Equal("Z Rede com problema", saude.Redes[0].Nome);
        Assert.True(saude.Redes[0].Atencao);
        Assert.Equal(1, saude.Redes[0].AvisosAbertos);
        SaudeEmpresaDto sul = saude.Redes[0].Empresas.Single(e => e.Nome == "Loja Sul");
        Assert.False(sul.BlingConectado);
        Assert.Null(sul.TokenValidoAte);
        Assert.Equal(1, sul.AvisosAbertos);
        Assert.False(saude.Redes[1].Atencao);
        Assert.Equal(new DateTime(2026, 11, 11, 18, 0, 0, DateTimeKind.Utc), saude.Redes[1].Empresas[0].TokenValidoAte);
    }

    [Fact]
    public async Task Conta_as_notas_aguardando_e_com_problema_por_rede_e_empresa()
    {
        Cliente rede = Rede("Rede");
        Empresa centro = Empresa(rede, "Centro", true);
        await _db.SaveChangesAsync();
        Nota(rede, centro, StatusNota.Recebida, new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));
        Nota(rede, centro, StatusNota.Recebida, new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc));
        Nota(rede, centro, StatusNota.Erro, new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc));
        Nota(rede, centro, StatusNota.Lancada, new DateTime(2026, 11, 8, 9, 0, 0, DateTimeKind.Utc));
        Nota(rede, null, StatusNota.EmpresaNaoEncontrada, new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc));
        await _db.SaveChangesAsync();

        SaudeRedeDto saude = (await CriarServico().ObterAsync(CancellationToken.None)).Redes.Single();

        Assert.Equal(2, saude.NotasAguardando);
        Assert.Equal(2, saude.NotasComProblema);
        Assert.Equal(new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc), saude.UltimaNotaEm);
        Assert.Equal(2, saude.Empresas[0].NotasAguardando);
        Assert.Equal(1, saude.Empresas[0].NotasComProblema);
    }

    [Fact]
    public async Task Caixa_falhando_so_quando_a_falha_e_mais_nova_que_a_ultima_leitura()
    {
        Cliente rede = Rede("Rede");
        rede.ImapUltimaConexaoEm = new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);
        rede.ImapUltimaFalhaConexaoEm = new DateTime(2026, 11, 11, 11, 0, 0, DateTimeKind.Utc);
        rede.ImapUltimoErroConexao = "Usuário ou senha recusados";
        Empresa propria = Empresa(rede, "Filial", true);
        propria.ImapHost = "imap.gmail.com";
        propria.ImapUsuario = "filial@gmail.com";
        propria.ImapUltimaFalhaConexaoEm = new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);
        propria.ImapUltimoErroConexao = "Tempo esgotado";
        propria.ImapUltimaConexaoEm = new DateTime(2026, 11, 11, 11, 50, 0, DateTimeKind.Utc);
        Empresa(rede, "Matriz", true);
        await _db.SaveChangesAsync();

        SaudeRedeDto saude = (await CriarServico().ObterAsync(CancellationToken.None)).Redes.Single();

        Assert.True(saude.Caixa!.Falhando);
        Assert.Equal("Usuário ou senha recusados", saude.Caixa.UltimoErro);
        SaudeCaixaDto filial = saude.Empresas.Single(e => e.Nome == "Filial").Caixa!;
        Assert.False(filial.Falhando);
        Assert.Null(filial.UltimoErro);
        Assert.Equal("filial@gmail.com", filial.Usuario);
        Assert.Null(saude.Empresas.Single(e => e.Nome == "Matriz").Caixa);
        Assert.True(saude.Atencao);
    }

    [Fact]
    public async Task Jobs_na_ordem_da_tela_com_a_situacao_traduzida()
    {
        _agenda.Lista.Add(new ExecucaoAgendada { Id = "sincronizacao", UltimaSituacao = "Succeeded", UltimaExecucaoEm = DateTime.UtcNow, Erro = "ignorado" });
        _agenda.Lista.Add(new ExecucaoAgendada { Id = "leitura-email", UltimaSituacao = "Failed", UltimaExecucaoEm = DateTime.UtcNow, Erro = "Banco fora do ar" });
        _agenda.Lista.Add(new ExecucaoAgendada { Id = "lancamento", UltimaSituacao = "Scheduled", UltimaExecucaoEm = DateTime.UtcNow, Erro = "Bling 503" });
        _agenda.Lista.Add(new ExecucaoAgendada { Id = "resumo-semanal" });
        _agenda.Lista.Add(new ExecucaoAgendada { Id = "outro-job", UltimaSituacao = "Processing", UltimaExecucaoEm = DateTime.UtcNow });

        List<SaudeJobDto> jobs = (await CriarServico().ObterAsync(CancellationToken.None)).Jobs;

        Assert.Equal(new List<string> { "leitura-email", "lancamento", "sincronizacao", "resumo-semanal", "outro-job" }, jobs.Select(j => j.Id).ToList());
        Assert.Equal("Leitura dos e-mails", jobs[0].Nome);
        Assert.Equal("Falhou", jobs[0].Situacao);
        Assert.Equal("Banco fora do ar", jobs[0].Erro);
        Assert.Equal("TentandoDeNovo", jobs[1].Situacao);
        Assert.Equal("Ok", jobs[2].Situacao);
        Assert.Null(jobs[2].Erro);
        Assert.Equal("NuncaRodou", jobs[3].Situacao);
        Assert.Equal("Rodando", jobs[4].Situacao);
        Assert.Equal("outro-job", jobs[4].Nome);
    }
}
