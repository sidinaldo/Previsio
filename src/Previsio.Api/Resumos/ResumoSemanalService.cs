using Microsoft.EntityFrameworkCore;
using Previsio.Api.Contratos;
using Previsio.Api.Avisos;
using Previsio.Api.Dtos;
using Previsio.Api.Servicos;
using Previsio.Core.Abstracoes;
using Previsio.Core.Entidades;
using Previsio.Core.Contratos;
using Previsio.Core.Comum;
using Previsio.Aplicacao.Comum;
using Previsio.Aplicacao.Abstracoes;

namespace Previsio.Api.Resumos;

/// <summary>
/// Resumo semanal por e-mail: vencimentos dos próximos 7 dias, total dos 30, notas da semana
/// que passou e problemas em aberto. Cada rede escolhe o dia e a hora; vai para os usuários dela.
/// </summary>
public class ResumoSemanalService
{
    public const string MensagemDiaInvalido = "Escolha um dia da semana ou todo dia útil.";

    // Valor de "Dia" para mandar de segunda a sexta (0 a 6 são os dias da semana, como DayOfWeek)
    public const int TodoDiaUtil = 7;
    public const string MensagemHoraInvalida = "Escolha uma hora entre 0 e 23.";
    public const string MensagemSmtpNaoConfigurado = "O envio de e-mail não está configurado no servidor (seção Smtp).";
    public const string MensagemFalhaEnvio = "Não consegui enviar o e-mail. Confira a configuração do Smtp no servidor.";

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Usuario> _usuarios;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<ContaPagarSincronizada> _contasPagar;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IRepositorio<Aviso> _registrosAviso;
    private readonly AvisosService _avisos;
    private readonly IEnvioEmail _email;
    private readonly IConfiguration _configuracao;
    private readonly TimeProvider _relogio;
    private readonly ILogger<ResumoSemanalService> _logger;

    public ResumoSemanalService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Usuario> usuarios,
        IRepositorio<Empresa> empresas,
        IRepositorio<ContaPagarSincronizada> contasPagar,
        IRepositorio<NotaFiscal> notas,
        IRepositorio<Aviso> registrosAviso,
        AvisosService avisos,
        IEnvioEmail email,
        IConfiguration configuracao,
        TimeProvider relogio,
        ILogger<ResumoSemanalService> logger)
    {
        _clientes = clientes;
        _usuarios = usuarios;
        _empresas = empresas;
        _contasPagar = contasPagar;
        _notas = notas;
        _registrosAviso = registrosAviso;
        _avisos = avisos;
        _email = email;
        _configuracao = configuracao;
        _relogio = relogio;
        _logger = logger;
    }

    public async Task<Resultado<ResumoSemanalDto>> ObterAsync(int clienteId, CancellationToken cancelamento)
    {
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ResumoSemanalDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        return Resultado<ResumoSemanalDto>.Ok(await ParaDtoAsync(cliente, cancelamento));
    }

    public async Task<Resultado<ResumoSemanalDto>> AlterarAsync(
        int clienteId, AlteracaoResumoSemanalDto dados, CancellationToken cancelamento)
    {
        if (dados.Dia < 0 || dados.Dia > TodoDiaUtil)
        {
            return Resultado<ResumoSemanalDto>.Invalido(MensagemDiaInvalido);
        }
        if (dados.Hora < 0 || dados.Hora > 23)
        {
            return Resultado<ResumoSemanalDto>.Invalido(MensagemHoraInvalida);
        }

        Cliente? cliente = await _clientes.Consultar().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado<ResumoSemanalDto>.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }

        cliente.ResumoSemanalAtivo = dados.Ativo;
        cliente.ResumoSemanalDia = dados.Dia;
        cliente.ResumoSemanalHora = dados.Hora;
        await _clientes.SalvarAsync(cancelamento);
        return Resultado<ResumoSemanalDto>.Ok(await ParaDtoAsync(cliente, cancelamento));
    }

    /// <summary>
    /// Manda o resumo da rede só para quem pediu, para conferir como ele chega. Não conta como o envio da semana.
    /// </summary>
    public async Task<Resultado> EnviarTesteAsync(int clienteId, int usuarioId, CancellationToken cancelamento)
    {
        if (!_email.Configurado)
        {
            return Resultado.Invalido(MensagemSmtpNaoConfigurado);
        }
        Cliente? cliente = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == clienteId, cancelamento);
        if (cliente == null)
        {
            return Resultado.NaoEncontrado(ClientesService.MensagemNaoEncontrado);
        }
        Usuario? usuario = await _usuarios.ConsultarSomenteLeitura().FirstOrDefaultAsync(u => u.Id == usuarioId, cancelamento);
        if (usuario == null)
        {
            return Resultado.NaoEncontrado(null);
        }

        ResumoSemanal resumo = await MontarAsync(cliente, cancelamento);
        try
        {
            await _email.EnviarAsync(ModeloEmailResumo.Montar(resumo, usuario, UrlFront()), cancelamento);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            _logger.LogError(erro, "Falha ao enviar o resumo semanal de teste da rede {ClienteId}", clienteId);
            return Resultado.Invalido(MensagemFalhaEnvio);
        }
        return Resultado.Ok();
    }

    /// <summary>
    /// Chamado de hora em hora: envia o resumo das redes cujo dia chegou (o dia da semana escolhido
    /// ou qualquer dia útil) e cuja hora já passou, uma vez por dia. Se o servidor ficou fora na hora marcada, envia na primeira hora seguinte do mesmo dia.
    /// </summary>
    public async Task EnviarDevidosAsync(CancellationToken cancelamento)
    {
        DateTime agoraUtc = _relogio.GetUtcNow().UtcDateTime;
        DateTime agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, RelogioBrasil.Fuso);

        int diaDaSemana = (int)agoraLocal.DayOfWeek;
        bool diaUtil = agoraLocal.DayOfWeek != DayOfWeek.Saturday && agoraLocal.DayOfWeek != DayOfWeek.Sunday;
        List<int> redes = await _clientes.Consultar().Where(PeriodoTeste.Processa(agoraUtc)).Select(c => c.Id).ToListAsync(cancelamento);
        List<Cliente> clientes = await _clientes.Consultar()
            .Where(c => redes.Contains(c.Id)
                && c.ResumoSemanalAtivo
                && (c.ResumoSemanalDia == diaDaSemana || (diaUtil && c.ResumoSemanalDia == TodoDiaUtil))
                && c.ResumoSemanalHora <= agoraLocal.Hour)
            .ToListAsync(cancelamento);

        foreach (Cliente cliente in clientes)
        {
            if (JaEnviadoHoje(cliente, agoraLocal))
            {
                continue;
            }

            if (!_email.Configurado)
            {
                _logger.LogWarning("Smtp não configurado: resumo semanal da rede {ClienteId} não enviado", cliente.Id);
                cliente.ResumoSemanalEnviadoEm = agoraUtc;
                continue;
            }

            List<Usuario> usuarios = await DestinatariosAsync(cliente.Id, cancelamento);
            if (usuarios.Count > 0)
            {
                ResumoSemanal resumo = await MontarAsync(cliente, cancelamento);
                foreach (Usuario usuario in usuarios)
                {
                    try
                    {
                        await _email.EnviarAsync(ModeloEmailResumo.Montar(resumo, usuario, UrlFront()), cancelamento);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception erro)
                    {
                        // Um destinatário com problema não impede os outros
                        _logger.LogError(erro, "Falha ao enviar o resumo semanal ao usuário {UsuarioId}", usuario.Id);
                    }
                }
            }
            cliente.ResumoSemanalEnviadoEm = agoraUtc;
        }
        await _clientes.SalvarAsync(cancelamento);
    }

    /// <summary>
    /// Calcula os números do resumo de uma rede (vencimentos em aberto no Bling, notas e avisos).
    /// </summary>
    public async Task<ResumoSemanal> MontarAsync(Cliente cliente, CancellationToken cancelamento)
    {
        DateTime agoraUtc = _relogio.GetUtcNow().UtcDateTime;
        DateTime hoje = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, RelogioBrasil.Fuso).Date;

        var resumo = new ResumoSemanal();
        resumo.ClienteNome = cliente.Nome;
        resumo.Hoje = hoje;

        var empresas = await _empresas.ConsultarSomenteLeitura()
            .Where(e => e.ClienteId == cliente.Id && e.Ativo)
            .Select(e => new { e.Id, e.Nome })
            .ToListAsync(cancelamento);
        List<int> idsEmpresas = empresas.Select(e => e.Id).ToList();

        DateTime fim30 = hoje.AddDays(30);
        DateTime fim7 = hoje.AddDays(6);
        List<ContaPagarSincronizada> contas = await _contasPagar.ConsultarSomenteLeitura()
            .Where(c => idsEmpresas.Contains(c.EmpresaId)
                && c.Vencimento >= hoje
                && c.Vencimento <= fim30
                && (c.Situacao == SituacaoContaBling.EmAberto || c.Situacao == SituacaoContaBling.ParcialmentePago))
            .ToListAsync(cancelamento);

        List<ContaPagarSincronizada> vencidas = await Vencimentos.ContasEmAberto.Vencidas(_contasPagar.ConsultarSomenteLeitura(), idsEmpresas, hoje, 1).ToListAsync(cancelamento);
        resumo.Vencidas = vencidas.Count;
        resumo.TotalVencidas = vencidas.Sum(c => c.Valor);

        resumo.Total30Dias = contas.Sum(c => c.Valor);
        resumo.Contas30Dias = contas.Count;

        List<ContaPagarSincronizada> semana = contas.Where(c => c.Vencimento.Date <= fim7).ToList();
        resumo.Total7Dias = semana.Sum(c => c.Valor);
        foreach (var grupoDia in semana.GroupBy(c => c.Vencimento.Date).OrderBy(g => g.Key))
        {
            var dia = new VencimentoDoDia();
            dia.Data = grupoDia.Key;
            dia.Total = grupoDia.Sum(c => c.Valor);
            foreach (var grupoEmpresa in grupoDia.GroupBy(c => c.EmpresaId))
            {
                var empresa = new VencimentoDaEmpresa();
                var dadosEmpresa = empresas.FirstOrDefault(e => e.Id == grupoEmpresa.Key);
                if (dadosEmpresa != null)
                {
                    empresa.Empresa = dadosEmpresa.Nome;
                }
                empresa.Quantidade = grupoEmpresa.Count();
                empresa.Valor = grupoEmpresa.Sum(c => c.Valor);
                dia.Empresas.Add(empresa);
            }
            dia.Empresas = dia.Empresas.OrderBy(e => e.Empresa).ToList();
            resumo.Dias.Add(dia);
        }

        // Notas desde o último resumo: o diário mostra o que chegou de um envio para o outro, sem repetir
        resumo.Diario = cliente.ResumoSemanalDia == TodoDiaUtil;
        DateTime notasDesdeUtc = InicioDasNotas(cliente, agoraUtc);
        resumo.NotasDesde = TimeZoneInfo.ConvertTimeFromUtc(notasDesdeUtc, RelogioBrasil.Fuso);
        List<NotaFiscal> notas = await _notas.ConsultarSomenteLeitura()
            .Where(n => n.ClienteId == cliente.Id && n.RecebidaEm >= notasDesdeUtc && n.RecebidaEm < agoraUtc)
            .ToListAsync(cancelamento);
        resumo.NotasRecebidas = notas.Count;
        resumo.ValorNotasRecebidas = notas.Sum(n => n.ValorTotal);
        resumo.NotasLancadas = notas.Count(n => n.Status == StatusNota.Lancada || n.Status == StatusNota.LancadaManualmente);
        resumo.NotasComProblema = notas.Count(n => StatusComProblema.Todos.Contains(n.Status));

        await _avisos.AtualizarAsync(cliente.Id, cancelamento);
        resumo.Problemas = await _registrosAviso.ConsultarSomenteLeitura()
            .Where(a => a.ClienteId == cliente.Id && a.ResolvidoEm == null)
            .OrderByDescending(a => a.CriadoEm)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancelamento);

        return resumo;
    }

    /// <summary>
    /// Início do trecho de notas: o último envio, se foi nos últimos 7 dias; sem envio recente,
    /// 1 dia para o resumo diário e 7 para o semanal.
    /// </summary>
    public static DateTime InicioDasNotas(Cliente cliente, DateTime agoraUtc)
    {
        DateTime limite = agoraUtc.AddDays(-7);
        if (cliente.ResumoSemanalEnviadoEm.HasValue)
        {
            DateTime enviado = DateTime.SpecifyKind(cliente.ResumoSemanalEnviadoEm.Value, DateTimeKind.Utc);
            if (enviado > limite && enviado < agoraUtc)
            {
                return enviado;
            }
        }
        if (cliente.ResumoSemanalDia == TodoDiaUtil)
        {
            return agoraUtc.AddDays(-1);
        }
        return limite;
    }

    private static bool JaEnviadoHoje(Cliente cliente, DateTime agoraLocal)
    {
        if (!cliente.ResumoSemanalEnviadoEm.HasValue)
        {
            return false;
        }
        DateTime enviadoUtc = DateTime.SpecifyKind(cliente.ResumoSemanalEnviadoEm.Value, DateTimeKind.Utc);
        DateTime enviadoLocal = TimeZoneInfo.ConvertTimeFromUtc(enviadoUtc, RelogioBrasil.Fuso);
        return enviadoLocal.Date == agoraLocal.Date;
    }

    private Task<List<Usuario>> DestinatariosAsync(int clienteId, CancellationToken cancelamento)
    {
        return _usuarios.ConsultarSomenteLeitura()
            .Where(u => u.Ativo && u.Perfil == PerfilUsuario.Cliente && u.ClienteId == clienteId)
            .OrderBy(u => u.Id)
            .ToListAsync(cancelamento);
    }

    private async Task<ResumoSemanalDto> ParaDtoAsync(Cliente cliente, CancellationToken cancelamento)
    {
        var dto = new ResumoSemanalDto();
        dto.Ativo = cliente.ResumoSemanalAtivo;
        dto.Dia = cliente.ResumoSemanalDia;
        dto.Hora = cliente.ResumoSemanalHora;
        dto.UltimoEnvioEm = cliente.ResumoSemanalEnviadoEm;
        dto.Destinatarios = (await DestinatariosAsync(cliente.Id, cancelamento)).Count;
        return dto;
    }

    private string UrlFront()
    {
        return (_configuracao["Front:Url"] ?? "http://localhost:4200/#").TrimEnd('/');
    }
}
