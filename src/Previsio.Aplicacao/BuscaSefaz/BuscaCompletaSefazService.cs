using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Previsio.Core.Abstracoes;
using Previsio.Core.Contratos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;
using Previsio.Core.Sefaz;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.Certificados;
using Previsio.Aplicacao.Notas;

namespace Previsio.Aplicacao.BuscaSefaz;

/// <summary>
/// Busca completa na SEFAZ (distribuição por NSU): toda NF-e emitida contra o CNPJ de cada empresa,
/// mesmo a que o fornecedor não manda por e-mail.
///
/// - A primeira busca da empresa pula o histórico (a SEFAZ guarda até 90 dias): só notas de agora em diante.
/// - Resumo de nota (resNFe): registra a Ciência da Operação; o XML completo vem numa busca seguinte.
/// - XML completo (procNFe) em que a empresa é a destinatária: vira nota pelo mesmo caminho do e-mail
///   (a chave de acesso evita duplicar a nota que também chegou por e-mail).
/// - Cancelamento: a nota ainda não lançada não vai para o Bling.
/// - Sem nada novo, a empresa só é consultada de novo depois do intervalo (a SEFAZ bloqueia por 1 hora).
/// </summary>
public class BuscaCompletaSefazService
{
    public const string OrigemSefaz = "SEFAZ (busca completa)";
    private static readonly TimeSpan EsperaConsumoIndevido = TimeSpan.FromHours(1);

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly ISefaz _sefaz;
    private readonly CertificadosService _certificados;
    private readonly RegistroNotaService _registro;
    private readonly LeitorNFe _leitor;
    private readonly TimeProvider _relogio;
    private readonly BuscaSefazOptions _opcoes;
    private readonly ILogger<BuscaCompletaSefazService> _logger;

    public BuscaCompletaSefazService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Empresa> empresas,
        IRepositorio<NotaFiscal> notas,
        ISefaz sefaz,
        CertificadosService certificados,
        RegistroNotaService registro,
        LeitorNFe leitor,
        TimeProvider relogio,
        IOptions<BuscaSefazOptions> opcoes,
        ILogger<BuscaCompletaSefazService> logger)
    {
        _clientes = clientes;
        _empresas = empresas;
        _notas = notas;
        _sefaz = sefaz;
        _certificados = certificados;
        _registro = registro;
        _leitor = leitor;
        _relogio = relogio;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    /// <summary>
    /// Consulta as empresas com a busca ligada, certificado (próprio ou da matriz) e hora de buscar chegada.
    /// </summary>
    public async Task ExecutarAsync(CancellationToken cancelamento)
    {
        DateTime agora = Agora();
        List<int> redes = await _clientes.Consultar()
            .Where(PeriodoTeste.Processa(agora))
            .Select(c => c.Id)
            .ToListAsync(cancelamento);
        List<Empresa> empresas = await _empresas.Consultar()
            .Where(e => e.Ativo && redes.Contains(e.ClienteId))
            .OrderBy(e => e.Id)
            .ToListAsync(cancelamento);

        var certificados = new Dictionary<int, X509Certificate2?>();
        try
        {
            foreach (Empresa empresa in empresas)
            {
                if (!empresa.SefazBuscaAtiva)
                {
                    continue;
                }
                if (empresa.SefazProximaBuscaEm.HasValue && empresa.SefazProximaBuscaEm.Value > agora)
                {
                    continue;
                }
                Empresa? dono = DonoDoCertificado(empresa, empresas);
                if (dono == null)
                {
                    continue;
                }
                await BuscarAsync(empresa, dono, empresas, certificados, agora, cancelamento);
                await _empresas.SalvarAsync(cancelamento);
            }
        }
        finally
        {
            foreach (X509Certificate2? certificado in certificados.Values)
            {
                if (certificado != null)
                {
                    certificado.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// O certificado da própria empresa ou o de outra empresa da mesma rede com a mesma raiz de CNPJ (matriz):
    /// a SEFAZ aceita consultar qualquer CNPJ da raiz do certificado.
    /// </summary>
    public static Empresa? DonoDoCertificado(Empresa empresa, List<Empresa> empresas)
    {
        if (empresa.TemCertificado)
        {
            return empresa;
        }
        string raiz = Raiz(empresa.Cnpj);
        return empresas.FirstOrDefault(e => e.ClienteId == empresa.ClienteId && e.TemCertificado && Raiz(e.Cnpj) == raiz);
    }

    private async Task BuscarAsync(
        Empresa empresa, Empresa dono, List<Empresa> empresas, Dictionary<int, X509Certificate2?> certificados,
        DateTime agora, CancellationToken cancelamento)
    {
        string? uf = empresa.Uf;
        if (!UfIbge.Valida(uf))
        {
            uf = dono.Uf;
        }
        if (!UfIbge.Valida(uf))
        {
            Concluir(empresa, agora, "Falta a UF da empresa para consultar a SEFAZ.");
            return;
        }
        if (!certificados.ContainsKey(dono.Id))
        {
            certificados[dono.Id] = _certificados.Abrir(dono);
        }
        X509Certificate2? certificado = certificados[dono.Id];
        if (certificado == null)
        {
            Concluir(empresa, agora, "O certificado de " + dono.Nome + " não abre (senha ou arquivo). Envie de novo.");
            return;
        }
        if (certificado.NotAfter.ToUniversalTime() < agora)
        {
            Concluir(empresa, agora, "O certificado de " + dono.Nome + " venceu em " + certificado.NotAfter.ToString("dd/MM/yyyy") + ".");
            return;
        }

        try
        {
            if (empresa.SefazUltimoNsu == null)
            {
                await ComecarAsync(empresa, certificado, uf!, agora, cancelamento);
                return;
            }
            await LerNovidadesAsync(empresa, certificado, uf!, agora, cancelamento);
        }
        catch (HttpRequestException erro)
        {
            _logger.LogWarning(erro, "Empresa {EmpresaId}: sem conexão com a SEFAZ", empresa.Id);
            Concluir(empresa, agora, "Sem conexão com a SEFAZ: " + erro.Message);
        }
        catch (TaskCanceledException erro)
        {
            if (cancelamento.IsCancellationRequested)
            {
                throw;
            }
            _logger.LogWarning(erro, "Empresa {EmpresaId}: a SEFAZ não respondeu a tempo", empresa.Id);
            Concluir(empresa, agora, "A SEFAZ não respondeu a tempo. Tento de novo na próxima busca.");
        }
    }

    // Primeira busca: só descobre o NSU mais novo e começa dali (o histórico fica de fora)
    private async Task ComecarAsync(Empresa empresa, X509Certificate2 certificado, string uf, DateTime agora, CancellationToken cancelamento)
    {
        RetornoSefaz retorno = await _sefaz.DistribuirPorNsuAsync(certificado, uf, empresa.Cnpj, "0", cancelamento);
        if (retorno.ConsumoIndevido)
        {
            Pausar(empresa, agora);
            return;
        }
        if (!retorno.DocumentoLocalizado && !retorno.NenhumDocumento)
        {
            Concluir(empresa, agora, "A SEFAZ recusou a consulta: " + retorno.Descricao);
            return;
        }
        string inicio = retorno.MaximoNsu;
        if (inicio == "")
        {
            inicio = retorno.UltimoNsu;
        }
        if (inicio == "")
        {
            inicio = "0";
        }
        empresa.SefazUltimoNsu = inicio;
        Concluir(empresa, agora, "Busca ligada: as notas emitidas para este CNPJ a partir de agora chegam sozinhas.");
    }

    private async Task LerNovidadesAsync(Empresa empresa, X509Certificate2 certificado, string uf, DateTime agora, CancellationToken cancelamento)
    {
        var contagem = new ContagemBuscaSefaz();
        int maximo = _opcoes.MaximoConsultasPorExecucaoValido();
        for (int consulta = 0; consulta < maximo; consulta++)
        {
            RetornoSefaz retorno = await _sefaz.DistribuirPorNsuAsync(certificado, uf, empresa.Cnpj, empresa.SefazUltimoNsu!, cancelamento);
            if (retorno.ConsumoIndevido)
            {
                Pausar(empresa, agora);
                return;
            }
            if (retorno.NenhumDocumento)
            {
                if (retorno.UltimoNsu != "")
                {
                    empresa.SefazUltimoNsu = retorno.UltimoNsu;
                }
                Concluir(empresa, agora, contagem.Resumo());
                return;
            }
            if (!retorno.DocumentoLocalizado)
            {
                Concluir(empresa, agora, "A SEFAZ recusou a consulta: " + retorno.Descricao);
                return;
            }

            foreach (DocumentoDistribuido documento in retorno.Documentos.OrderBy(d => d.Nsu))
            {
                bool seguir = await ProcessarAsync(documento, empresa, certificado, contagem, cancelamento);
                if (!seguir)
                {
                    // Para no documento que falhou: a próxima busca recomeça dele
                    Pausar(empresa, agora);
                    empresa.SefazSituacao = Limitar(contagem.Resumo() + " A SEFAZ pediu para esperar; continuo em 1 hora.");
                    return;
                }
                if (documento.Nsu != "")
                {
                    empresa.SefazUltimoNsu = documento.Nsu;
                }
            }
            if (retorno.UltimoNsu != "")
            {
                empresa.SefazUltimoNsu = retorno.UltimoNsu;
            }
            if (!retorno.TemMais)
            {
                Concluir(empresa, agora, contagem.Resumo());
                return;
            }
        }

        // Ainda há documentos: continua na próxima execução, sem esperar o intervalo
        empresa.SefazUltimaBuscaEm = agora;
        empresa.SefazProximaBuscaEm = agora;
        empresa.SefazSituacao = Limitar(contagem.Resumo() + " Ainda há documentos; continuo na próxima execução.");
    }

    /// <summary>Falso para parar a busca da empresa neste documento (a SEFAZ pediu para esperar).</summary>
    private async Task<bool> ProcessarAsync(
        DocumentoDistribuido documento, Empresa empresa, X509Certificate2 certificado, ContagemBuscaSefaz contagem,
        CancellationToken cancelamento)
    {
        if (documento.NotaCompleta)
        {
            await RegistrarNotaAsync(documento, empresa, contagem, cancelamento);
            return true;
        }
        if (documento.ResumoDaNota)
        {
            return await ManifestarAsync(documento, empresa, certificado, contagem, cancelamento);
        }
        if (documento.EventoDaNota)
        {
            await AplicarEventoAsync(documento, empresa, contagem, cancelamento);
        }
        return true;
    }

    private async Task RegistrarNotaAsync(
        DocumentoDistribuido documento, Empresa empresa, ContagemBuscaSefaz contagem, CancellationToken cancelamento)
    {
        NotaFiscalXml? nota = _leitor.Ler(documento.Xml);
        if (nota == null)
        {
            _logger.LogWarning("Empresa {EmpresaId}: XML da SEFAZ que não consegui ler (NSU {Nsu})", empresa.Id, documento.Nsu);
            return;
        }
        // A distribuição também traz notas em que o CNPJ é só transportador ou autorizado: essas não são contas a pagar
        if (nota.CnpjDestinatario != empresa.Cnpj)
        {
            return;
        }
        ResultadoRegistro resultado = await _registro.RegistrarAsync(empresa.ClienteId, nota, OrigemSefaz, null, cancelamento);
        if (resultado == ResultadoRegistro.Registrada)
        {
            contagem.NotasNovas++;
        }
    }

    private async Task<bool> ManifestarAsync(
        DocumentoDistribuido documento, Empresa empresa, X509Certificate2 certificado, ContagemBuscaSefaz contagem,
        CancellationToken cancelamento)
    {
        DocumentoSefazLido? resumo = DocumentoSefazLido.Ler(documento.Xml);
        if (resumo == null || resumo.Chave.Length != 44)
        {
            return true;
        }
        if (resumo.Situacao == DocumentoSefazLido.SituacaoCancelada)
        {
            return true;
        }
        // Já chegou por e-mail (ou pelo PDF): não precisa do XML da SEFAZ
        int clienteId = empresa.ClienteId;
        string chave = resumo.Chave;
        bool jaTem = await _notas.Consultar().AnyAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == chave, cancelamento);
        if (jaTem)
        {
            return true;
        }

        RetornoSefaz evento = await _sefaz.ManifestarCienciaAsync(certificado, empresa.Cnpj, chave, cancelamento);
        if (evento.EventoRegistrado)
        {
            contagem.Ciencias++;
            return true;
        }
        if (evento.ConsumoIndevido)
        {
            return false;
        }
        _logger.LogWarning("Empresa {EmpresaId}: a SEFAZ recusou a ciência da nota {Chave}: {Motivo}", empresa.Id, chave, evento.Descricao);
        contagem.CienciasRecusadas++;
        return true;
    }

    private async Task AplicarEventoAsync(
        DocumentoDistribuido documento, Empresa empresa, ContagemBuscaSefaz contagem, CancellationToken cancelamento)
    {
        DocumentoSefazLido? evento = DocumentoSefazLido.Ler(documento.Xml);
        if (evento == null || !evento.Cancelamento || evento.Chave == "")
        {
            return;
        }
        int clienteId = empresa.ClienteId;
        string chave = evento.Chave;
        NotaFiscal? nota = await _notas.Consultar().FirstOrDefaultAsync(n => n.ClienteId == clienteId && n.ChaveAcesso == chave, cancelamento);
        if (nota == null)
        {
            return;
        }
        if (StatusPendentes.Todos.Contains(nota.Status))
        {
            nota.Status = StatusNota.CanceladaNaSefaz;
            nota.MensagemErro = "O emitente cancelou a nota na SEFAZ: não vai para o Bling.";
            contagem.Canceladas++;
            return;
        }
        if (nota.Status == StatusNota.Lancada || nota.Status == StatusNota.LancadaManualmente)
        {
            _logger.LogWarning("Empresa {EmpresaId}: a nota {Chave}, já lançada no Bling, foi cancelada pelo emitente", empresa.Id, chave);
            contagem.CanceladasDepoisDeLancadas++;
        }
    }

    private void Concluir(Empresa empresa, DateTime agora, string situacao)
    {
        empresa.SefazUltimaBuscaEm = agora;
        empresa.SefazProximaBuscaEm = agora.AddMinutes(_opcoes.IntervaloMinutosValido());
        empresa.SefazSituacao = Limitar(situacao);
    }

    // 656 (consumo indevido): a SEFAZ bloqueou o CNPJ por 1 hora
    private void Pausar(Empresa empresa, DateTime agora)
    {
        DateTime proxima = agora.Add(EsperaConsumoIndevido);
        DateTime pelaConfiguracao = agora.AddMinutes(_opcoes.IntervaloMinutosValido());
        if (pelaConfiguracao > proxima)
        {
            proxima = pelaConfiguracao;
        }
        empresa.SefazUltimaBuscaEm = agora;
        empresa.SefazProximaBuscaEm = proxima;
        empresa.SefazSituacao = "A SEFAZ pediu para esperar 1 hora (consumo indevido); a busca continua depois.";
    }

    private static string Limitar(string texto)
    {
        if (texto.Length <= 500)
        {
            return texto;
        }
        return texto.Substring(0, 500);
    }

    private static string Raiz(string cnpj)
    {
        if (cnpj.Length < 8)
        {
            return cnpj;
        }
        return cnpj.Substring(0, 8);
    }

    private DateTime Agora()
    {
        return _relogio.GetUtcNow().UtcDateTime;
    }
}
