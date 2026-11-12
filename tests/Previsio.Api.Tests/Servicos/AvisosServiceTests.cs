using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;
using Previsio.Api.Jobs;
using Previsio.Api.Servicos;
using Previsio.Api.Tests.Apoio;
using Previsio.Core.Entidades;
using Previsio.Data;
using Xunit;
using Previsio.Infra.Seguranca;

namespace Previsio.Api.Tests.Servicos;

public class AvisosServiceTests
{
    private readonly PrevisioDbContext _db = BancoEmMemoria.Criar();
    private readonly EnvioEmailDeTeste _email = new EnvioEmailDeTeste();
    private readonly RelogioDeTeste _relogio = new RelogioDeTeste();
    private readonly ProtetorSegredos _protetor = new ProtetorSegredos(new EphemeralDataProtectionProvider());

    private Cliente _rede = null!;
    private Empresa _conectada = null!;
    private Usuario _admin = null!;
    private Usuario _financeiro = null!;

    private AvisosService CriarServico()
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "Front:Url", "https://app.previsio.com.br/#" } })
            .Build();
        return new AvisosService(_db, _email, configuracao, _relogio, NullLogger<AvisosService>.Instance);
    }

    private async Task CadastrarRede()
    {
        _rede = new Cliente();
        _rede.Nome = "Rede Auto Center";
        _rede.ImapHost = "imap.gmail.com";
        _rede.ImapUsuario = "notas@autocenter.com.br";
        _db.Clientes.Add(_rede);
        await _db.SaveChangesAsync();

        _conectada = new Empresa();
        _conectada.ClienteId = _rede.Id;
        _conectada.Nome = "Auto Center Centro";
        _conectada.Cnpj = "11222333000262";
        _conectada.BlingAccessTokenProtegido = _protetor.Proteger("token");
        _conectada.BlingRefreshTokenProtegido = _protetor.Proteger("refresh");
        _conectada.BlingTokenExpiraEm = DateTime.UtcNow.AddDays(30);
        _db.Empresas.Add(_conectada);

        _admin = Usuario("admin@softio.com.br", PerfilUsuario.Admin, null);
        _financeiro = Usuario("ana@autocenter.com.br", PerfilUsuario.Cliente, _rede.Id);
        // Usuário de outra rede: não pode receber os avisos desta
        var outraRede = new Cliente();
        outraRede.Nome = "Outra rede";
        _db.Clientes.Add(outraRede);
        await _db.SaveChangesAsync();
        Usuario("joao@outra.com.br", PerfilUsuario.Cliente, outraRede.Id);
        await _db.SaveChangesAsync();
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

    private NotaFiscal Nota(string numero, StatusNota status, int tentativas = 0)
    {
        var nota = new NotaFiscal();
        nota.ClienteId = _rede.Id;
        nota.EmpresaId = _conectada.Id;
        nota.Numero = numero;
        nota.ChaveAcesso = "chave-" + numero;
        nota.NomeFornecedor = "GAMA PNEUS E RODAS LTDA";
        nota.CnpjDestinatario = "11222333000343";
        nota.Status = status;
        nota.Tentativas = tentativas;
        _db.NotasFiscais.Add(nota);
        return nota;
    }

    private List<Aviso> Abertos()
    {
        return _db.Avisos.Where(a => a.ResolvidoEm == null).OrderBy(a => a.Chave).ToList();
    }

    [Fact]
    public async Task Atualizar_AbreAvisoParaNotaComProblemaEmpresaSemBlingECaixaComErro()
    {
        await CadastrarRede();
        var semBling = new Empresa();
        semBling.ClienteId = _rede.Id;
        semBling.Nome = "Auto Center Zona Norte";
        semBling.Cnpj = "11222333000181";
        _db.Empresas.Add(semBling);
        Nota("83001", StatusNota.EmpresaNaoEncontrada);
        _rede.ImapUltimaConexaoEm = _relogio.Agora.UtcDateTime.AddHours(-2);
        _rede.ImapUltimaFalhaConexaoEm = _relogio.Agora.UtcDateTime.AddMinutes(-5);
        _rede.ImapUltimoErroConexao = "Falha ao entrar na caixa de e-mail (usuário ou senha)";
        await _db.SaveChangesAsync();

        await CriarServico().AtualizarAsync(null, CancellationToken.None);

        List<Aviso> abertos = Abertos();
        Assert.Equal(3, abertos.Count);
        Aviso nota = abertos.Single(a => a.Tipo == TipoAviso.NotaComProblema);
        Assert.Equal("NF 83001: CNPJ 11.222.333/0003-43 não cadastrado", nota.Titulo);
        Assert.Equal("/pages/notas-pendentes", nota.Link);
        Assert.Contains("Zona Norte", abertos.Single(a => a.Tipo == TipoAviso.BlingDesconectado).Titulo);
        Assert.Contains("usuário ou senha", abertos.Single(a => a.Tipo == TipoAviso.CaixaEmailFalhando).Mensagem);
    }

    [Fact]
    public async Task Atualizar_NotaComErroAindaSendoTentada_NaoGeraAviso()
    {
        await CadastrarRede();
        Nota("100", StatusNota.Erro, tentativas: LancamentoNotaJob.MaximoTentativas - 1);
        Nota("101", StatusNota.Erro, tentativas: LancamentoNotaJob.MaximoTentativas);
        Nota("102", StatusNota.Lancada);
        await _db.SaveChangesAsync();

        await CriarServico().AtualizarAsync(null, CancellationToken.None);

        Assert.Equal(new[] { "nota:" + _db.NotasFiscais.Single(n => n.Numero == "101").Id }, Abertos().Select(a => a.Chave));
    }

    [Fact]
    public async Task Atualizar_DuasVezes_NaoRepeteEFechaOQueFoiResolvido()
    {
        await CadastrarRede();
        NotaFiscal nota = Nota("81006", StatusNota.SemParcelas);
        await _db.SaveChangesAsync();
        AvisosService servico = CriarServico();

        await servico.AtualizarAsync(null, CancellationToken.None);
        await servico.AtualizarAsync(null, CancellationToken.None);
        Assert.Single(_db.Avisos);

        // Alguém lançou à mão no Bling
        nota.Status = StatusNota.LancadaManualmente;
        await _db.SaveChangesAsync();
        _relogio.Agora = _relogio.Agora.AddMinutes(10);
        await servico.AtualizarAsync(null, CancellationToken.None);

        Aviso aviso = _db.Avisos.Single();
        Assert.Equal(_relogio.Agora.UtcDateTime, aviso.ResolvidoEm);
        Assert.Empty(Abertos());
    }

    [Fact]
    public async Task Atualizar_CaixaQueVoltouAConectar_FechaOAviso()
    {
        await CadastrarRede();
        _rede.ImapUltimaFalhaConexaoEm = _relogio.Agora.UtcDateTime.AddMinutes(-20);
        await _db.SaveChangesAsync();
        await CriarServico().AtualizarAsync(null, CancellationToken.None);
        Assert.Single(Abertos());

        _rede.ImapUltimaConexaoEm = _relogio.Agora.UtcDateTime;
        await _db.SaveChangesAsync();
        await CriarServico().AtualizarAsync(null, CancellationToken.None);

        Assert.Empty(Abertos());
    }

    [Fact]
    public async Task EnviarNovos_UmEmailPorUsuarioSoComOQueEleVeEMarcaComoEnviado()
    {
        await CadastrarRede();
        Nota("81006", StatusNota.SemParcelas);
        Nota("81007", StatusNota.SemParcelas);
        await _db.SaveChangesAsync();
        AvisosService servico = CriarServico();
        await servico.AtualizarAsync(null, CancellationToken.None);

        await servico.EnviarNovosAsync(CancellationToken.None);

        Assert.Equal(new[] { "admin@softio.com.br", "ana@autocenter.com.br" }, _email.Enviados.Select(e => e.Para).OrderBy(e => e));
        Assert.Equal("Previsio: 2 avisos precisam de atenção", _email.Enviados[0].Assunto);
        Assert.Contains("https://app.previsio.com.br/#/pages/notas-pendentes", _email.Enviados[0].Html);
        Assert.All(_db.Avisos, a => Assert.NotNull(a.EmailEnviadoEm));

        // Nada novo: não manda de novo
        _email.Enviados.Clear();
        await servico.EnviarNovosAsync(CancellationToken.None);
        Assert.Empty(_email.Enviados);
    }

    [Fact]
    public async Task EnviarNovos_FalhaNoEnvio_NaoTravaEMarcaParaNaoRepetir()
    {
        await CadastrarRede();
        Nota("81006", StatusNota.SemParcelas);
        await _db.SaveChangesAsync();
        _email.Falhar = true;
        AvisosService servico = CriarServico();
        await servico.AtualizarAsync(null, CancellationToken.None);

        await servico.EnviarNovosAsync(CancellationToken.None);

        Assert.All(_db.Avisos, a => Assert.NotNull(a.EmailEnviadoEm));
    }

    [Fact]
    public async Task Painel_UsuarioDoClienteSoVeASuaRedeENovosZeramAoAbrir()
    {
        await CadastrarRede();
        Nota("81006", StatusNota.SemParcelas);
        await _db.SaveChangesAsync();
        AvisosService servico = CriarServico();

        PainelAvisosDto painel = (await servico.PainelAsync(_financeiro.Id, CancellationToken.None))!;
        Assert.Equal(1, painel.Total);
        Assert.Equal(1, painel.Novos);
        Assert.True(painel.Itens[0].Novo);
        Assert.Equal("Rede Auto Center", painel.Itens[0].ClienteNome);

        Usuario outro = _db.Usuarios.Single(u => u.Email == "joao@outra.com.br");
        Assert.Equal(0, (await servico.PainelAsync(outro.Id, CancellationToken.None))!.Total);

        _relogio.Agora = _relogio.Agora.AddMinutes(1);
        await servico.MarcarVistosAsync(_financeiro.Id, CancellationToken.None);
        painel = (await servico.PainelAsync(_financeiro.Id, CancellationToken.None))!;
        Assert.Equal(1, painel.Total);
        Assert.Equal(0, painel.Novos);
    }
}
