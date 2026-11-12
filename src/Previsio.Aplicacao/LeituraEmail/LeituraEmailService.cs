using Microsoft.EntityFrameworkCore;
using Previsio.Aplicacao.Abstracoes;
using Previsio.Aplicacao.EmailsIgnorados;
using Previsio.Aplicacao.Falhas;
using Previsio.Aplicacao.Notas;
using Previsio.Aplicacao.NotasSoPdf;
using Previsio.Core.Abstracoes;
using Previsio.Core.Contratos;
using Previsio.Core.Entidades;
using Previsio.Core.NFe;

namespace Previsio.Aplicacao.LeituraEmail;

/// <summary>
/// Lê a caixa de e-mail de cada rede (cliente) e as caixas próprias das empresas que tiverem,
/// extrai os XMLs de NF-e (inclusive dentro de .zip) e registra as notas novas.
/// E-mail sem XML mas com a nota em PDF vira uma "nota só em PDF", para buscar o XML na SEFAZ.
/// E-mail com anexo que não vira nada fica em "E-mails ignorados", com o motivo.
/// </summary>
public class LeituraEmailService
{
    public const int MaximoMensagensPorExecucao = 200;

    // A leitura roda a cada 10 minutos: a mesma falha de conexão só é registrada
    // de novo depois desse intervalo, para não encher a tela com o mesmo erro
    private static readonly TimeSpan IntervaloFalhaRepetida = TimeSpan.FromHours(1);

    // Motivo da última falha de conexão guardado na caixa (para o aviso)
    private const int TamanhoMaximoErroConexao = 500;

    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<Empresa> _empresas;
    private readonly IRepositorio<FalhaProcessamento> _falhas;
    private readonly ILeitorImap _imap;
    private readonly IProtetorSegredos _protetor;
    private readonly RegistroNotaService _registro;
    private readonly LeitorNFe _leitor;
    private readonly RegistroNotaSoPdf _notasSoPdf;
    private readonly EmailsIgnoradosService _ignorados;
    private readonly ILogger<LeituraEmailService> _logger;

    public LeituraEmailService(
        IRepositorio<Cliente> clientes,
        IRepositorio<Empresa> empresas,
        IRepositorio<FalhaProcessamento> falhas,
        ILeitorImap imap,
        IProtetorSegredos protetor,
        RegistroNotaService registro,
        LeitorNFe leitor,
        RegistroNotaSoPdf notasSoPdf,
        EmailsIgnoradosService ignorados,
        ILogger<LeituraEmailService> logger)
    {
        _clientes = clientes;
        _empresas = empresas;
        _falhas = falhas;
        _imap = imap;
        _protetor = protetor;
        _registro = registro;
        _leitor = leitor;
        _notasSoPdf = notasSoPdf;
        _ignorados = ignorados;
        _logger = logger;
    }

    /// <summary>
    /// Lê todas as caixas das redes que estão sendo processadas (contratadas ou em teste).
    /// </summary>
    public async Task LerTodasAsync(CancellationToken cancelamento)
    {
        try
        {
            await _ignorados.LimparAntigosAsync(cancelamento);
        }
        catch (Exception erro)
        {
            // Limpeza é só arrumação: não pode impedir a leitura
            _logger.LogWarning(erro, "Não foi possível apagar os e-mails ignorados antigos");
        }

        List<Cliente> clientes = await _clientes.Consultar()
            .Include(c => c.Empresas)
            .Where(PeriodoTeste.Processa(DateTime.UtcNow))
            .ToListAsync(cancelamento);

        foreach (Cliente cliente in clientes)
        {
            // Caixa da rede e caixas próprias das empresas
            foreach (CaixaParaLer caixa in CaixasDeEmail.Montar(cliente))
            {
                try
                {
                    await LerCaixaAsync(caixa, cancelamento);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception erro)
                {
                    // Uma caixa com problema não pode travar as demais
                    _logger.LogError(erro, "Erro ao ler a caixa de e-mail da {Caixa} ({Endereco})",
                        caixa.Descricao, FabricaFalha.EnderecoImap(caixa.Dados));
                }
            }
        }
    }

    public async Task LerCaixaAsync(CaixaParaLer caixaParaLer, CancellationToken cancelamento)
    {
        Cliente cliente = caixaParaLer.Rede;
        ICaixaEmail dados = caixaParaLer.Dados;

        // Uma falha em outra caixa descarta o que estava acompanhado; volta a acompanhar esta
        // para o último UID lido ser gravado
        AcompanharDonoDaCaixa(caixaParaLer);

        using ISessaoImap sessao = _imap.NovaSessao();
        List<long> encontrados;

        // Passo atual, para a falha dizer exatamente onde parou
        string passo = "abrir a senha do e-mail gravada no Previsio";
        try
        {
            string senha = _protetor.Revelar(dados.ImapSenhaProtegida);

            passo = "conectar ao servidor de e-mail";
            await sessao.ConectarAsync(dados.ImapHost, dados.ImapPorta, dados.ImapSsl, cancelamento);

            passo = "entrar na caixa de e-mail (usuário ou senha)";
            await sessao.EntrarAsync(dados.ImapUsuario, senha, cancelamento);

            passo = "abrir a caixa de entrada";
            await sessao.AbrirCaixaDeEntradaAsync(cancelamento);

            // Entrou: a caixa deixa de aparecer como "com erro" nos avisos
            dados.ImapUltimaConexaoEm = DateTime.UtcNow;
            await _falhas.SalvarAsync(cancelamento);

            passo = "procurar e-mails novos";
            long validadeAtual = sessao.ValidadeUids;
            if (ControleUidEmail.PrecisaRecomecar(dados.ImapUidValidity, dados.ImapUltimoUid, validadeAtual))
            {
                // Primeira leitura ou o servidor renumerou a caixa: lê os últimos dias.
                // Notas já registradas são reconhecidas pela chave de acesso e não duplicam.
                dados.ImapUidValidity = validadeAtual;
                dados.ImapUltimoUid = null;
                await _falhas.SalvarAsync(cancelamento);

                DateTime desde = DateTime.UtcNow.Date.AddDays(-ControleUidEmail.DiasPrimeiraLeitura);
                encontrados = await sessao.ProcurarRecebidosDesdeAsync(desde, cancelamento);

                long? proximo = sessao.ProximoUid;
                if (encontrados.Count == 0 && proximo.HasValue)
                {
                    // Nada nos últimos dias: começa a acompanhar a partir do próximo e-mail
                    dados.ImapUltimoUid = proximo.Value - 1;
                    await _falhas.SalvarAsync(cancelamento);
                }
            }
            else
            {
                encontrados = await sessao.ProcurarDepoisDoUidAsync(dados.ImapUltimoUid!.Value, cancelamento);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            await RegistrarFalhaDeConexaoAsync(caixaParaLer, "Falha ao " + passo + ": " + erro.Message, cancelamento);
            throw;
        }

        // Do mais antigo para o mais novo, para o último UID gravado sempre avançar
        List<long> paraLer = ControleUidEmail.ProximosParaLer(encontrados, dados.ImapUltimoUid, MaximoMensagensPorExecucao);

        int registradas = 0;
        int duplicadas = 0;
        int processadas = 0;
        int soPdf = 0;

        foreach (long uid in paraLer)
        {
            string? origem = null;
            try
            {
                EmailRecebido email = await sessao.ObterAsync(uid, cancelamento);
                origem = AnaliseEmail.Origem(email);

                List<AnexoXml> xmls = AnaliseEmail.ExtrairXmls(email);
                if (xmls.Count == 0)
                {
                    soPdf += await TratarSemXmlAsync(caixaParaLer, email, cancelamento);
                }

                foreach (AnexoXml anexo in xmls)
                {
                    NotaFiscalXml? nota = _leitor.Ler(anexo.Conteudo);
                    if (nota == null)
                    {
                        _falhas.Adicionar(FabricaFalha.DoXml(cliente.Id, caixaParaLer.EmpresaId, dados, anexo.Nome, origem));
                        await _falhas.SalvarAsync(cancelamento);
                        continue;
                    }

                    ResultadoRegistro resultado = await _registro.RegistrarAsync(cliente.Id, nota, origem, caixaParaLer.EmpresaId, cancelamento);
                    if (resultado == ResultadoRegistro.Registrada)
                    {
                        registradas++;
                    }
                    else
                    {
                        duplicadas++;
                    }
                }

                // Só avança depois de registrar, para não perder nota se der erro no meio
                dados.ImapUltimoUid = uid;
                await _falhas.SalvarAsync(cancelamento);
                processadas++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception erro)
            {
                _logger.LogError(erro, "Erro ao ler o e-mail {Uid} da {Caixa}", uid, caixaParaLer.Descricao);
                bool continuar = await TratarFalhaDoEmailAsync(caixaParaLer, uid, origem, erro, sessao.Conectado, cancelamento);
                if (!continuar)
                {
                    break;
                }
            }
        }

        if (!sessao.Conectado)
        {
            _logger.LogWarning("Conexão com a caixa de e-mail da {Caixa} caiu durante a leitura", caixaParaLer.Descricao);
            return;
        }

        await sessao.DesconectarAsync(cancelamento);

        _logger.LogInformation(
            "Caixa da {Caixa}: {Processadas} e-mails lidos, {Registradas} notas novas, {Duplicadas} já existentes, {SoPdf} só em PDF",
            caixaParaLer.Descricao, processadas, registradas, duplicadas, soPdf);
    }

    /// <summary>
    /// E-mail sem XML: a nota só em PDF vira pendência; o resto com anexo vai para "E-mails ignorados".
    /// Devolve quantas notas só em PDF foram registradas.
    /// </summary>
    private async Task<int> TratarSemXmlAsync(CaixaParaLer caixaParaLer, EmailRecebido email, CancellationToken cancelamento)
    {
        int clienteId = caixaParaLer.Rede.Id;
        int? empresaId = caixaParaLer.EmpresaId;

        EmailSemXml? semXml = AnaliseEmail.ComPdf(email);
        if (semXml == null)
        {
            if (email.Arquivos.Count > 0)
            {
                // Tem anexo, mas nenhum é XML, ZIP com XML ou PDF
                await _ignorados.RegistrarAsync(
                    AnaliseEmail.ParaRegistrar(email, clienteId, empresaId, AnaliseEmail.MotivoAnexoSemNota, null), cancelamento);
            }
            return 0;
        }

        ResultadoNotaSoPdf analise = await _notasSoPdf.AnalisarAsync(clienteId, empresaId, semXml, false, cancelamento);
        if (analise.MotivoIgnorado != null)
        {
            await _ignorados.RegistrarAsync(
                AnaliseEmail.ParaRegistrar(email, clienteId, empresaId, analise.MotivoIgnorado, analise.Pdf), cancelamento);
        }
        else if (analise.MotivoJaExiste != null)
        {
            // A nota já estava no Previsio: nada é criado, mas o e-mail fica visível com o porquê
            EmailParaRegistrar jaExiste = AnaliseEmail.ParaRegistrar(email, clienteId, empresaId, analise.MotivoJaExiste, analise.Pdf);
            jaExiste.NotaFiscalId = analise.NotaFiscalExistenteId;
            jaExiste.NumeroNota = analise.NumeroNotaExistente;
            await _ignorados.RegistrarAsync(jaExiste, cancelamento);
        }
        return analise.Registradas;
    }

    /// <summary>
    /// Registra a falha de um e-mail e decide se a leitura continua.
    /// Sem conexão ou com menos de 3 tentativas: para aqui e o mesmo e-mail é tentado na próxima execução.
    /// Na 3ª falha do mesmo e-mail: pula ele, para não travar os seguintes.
    /// </summary>
    private async Task<bool> TratarFalhaDoEmailAsync(
        CaixaParaLer caixaParaLer, long uid, string? origem, Exception erro, bool conectado, CancellationToken cancelamento)
    {
        Cliente cliente = caixaParaLer.Rede;
        ICaixaEmail dados = caixaParaLer.Dados;
        int? empresaId = caixaParaLer.EmpresaId;

        // Descarta o que ficou pela metade (nota, parcelas) antes de gravar a falha
        _falhas.DescartarAlteracoes();
        AcompanharDonoDaCaixa(caixaParaLer);

        string mensagem = "Falha ao ler o e-mail (UID " + uid + "): " + erro.Message;

        if (!conectado)
        {
            // Queda de conexão não é culpa do e-mail: não conta como tentativa dele
            _falhas.Adicionar(FabricaFalha.DoEmail(cliente.Id, empresaId, dados, mensagem, origem));
            await _falhas.SalvarAsync(cancelamento);
            return false;
        }

        // UIDs são por caixa: conta só as falhas desta caixa (rede ou empresa)
        int falhasAnteriores = await _falhas.Consultar().CountAsync(f =>
            f.ClienteId == cliente.Id
            && f.EmpresaId == empresaId
            && f.Etapa == EtapaFalha.LeituraEmail
            && f.UidEmail == uid, cancelamento);

        bool desistir = falhasAnteriores + 1 >= ControleUidEmail.MaximoTentativasPorEmail;
        if (desistir)
        {
            mensagem = mensagem + " O e-mail foi ignorado depois de "
                + ControleUidEmail.MaximoTentativasPorEmail + " tentativas; confira-o na caixa de e-mail.";
        }

        FalhaProcessamento falha = FabricaFalha.DoEmail(cliente.Id, empresaId, dados, mensagem, origem);
        falha.UidEmail = uid;
        _falhas.Adicionar(falha);

        if (desistir)
        {
            dados.ImapUltimoUid = uid;
        }

        await _falhas.SalvarAsync(cancelamento);
        return desistir;
    }

    /// <summary>
    /// Grava a falha de conexão com a caixa de e-mail, exceto se a mesma mensagem
    /// já foi registrada para essa caixa há menos de uma hora.
    /// </summary>
    private async Task RegistrarFalhaDeConexaoAsync(CaixaParaLer caixaParaLer, string mensagem, CancellationToken cancelamento)
    {
        int clienteId = caixaParaLer.Rede.Id;
        int? empresaId = caixaParaLer.EmpresaId;
        try
        {
            FalhaProcessamento falha = FabricaFalha.DoEmail(clienteId, empresaId, caixaParaLer.Dados, mensagem, null);
            DateTime limite = DateTime.UtcNow.Subtract(IntervaloFalhaRepetida);

            bool jaRegistrada = await _falhas.Consultar().AnyAsync(f =>
                f.ClienteId == clienteId
                && f.EmpresaId == empresaId
                && f.Etapa == EtapaFalha.LeituraEmail
                && f.Mensagem == falha.Mensagem
                && f.OcorridaEm >= limite, cancelamento);

            _falhas.DescartarAlteracoes();
            AcompanharDonoDaCaixa(caixaParaLer);

            // Marca a caixa como "com erro" (para os avisos) mesmo quando a falha repetida não é gravada de novo
            caixaParaLer.Dados.ImapUltimaFalhaConexaoEm = DateTime.UtcNow;
            caixaParaLer.Dados.ImapUltimoErroConexao = LimitarErro(mensagem);
            if (!jaRegistrada)
            {
                _falhas.Adicionar(falha);
            }
            await _falhas.SalvarAsync(cancelamento);
        }
        catch (Exception erro)
        {
            // Falha ao gravar o log não pode esconder o erro original
            _logger.LogError(erro, "Não foi possível registrar a falha de e-mail da {Caixa}", caixaParaLer.Descricao);
        }
    }

    /// <summary>
    /// Garante que a rede (e a empresa, quando a caixa é dela) está sendo acompanhada,
    /// para o último UID lido ser gravado depois de descartar alterações.
    /// </summary>
    private void AcompanharDonoDaCaixa(CaixaParaLer caixaParaLer)
    {
        _clientes.Acompanhar(caixaParaLer.Rede);
        if (caixaParaLer.Empresa != null)
        {
            _empresas.Acompanhar(caixaParaLer.Empresa);
        }
    }

    private static string LimitarErro(string mensagem)
    {
        if (mensagem.Length <= TamanhoMaximoErroConexao)
        {
            return mensagem;
        }
        return mensagem.Substring(0, TamanhoMaximoErroConexao);
    }
}
