using Hangfire;
using Microsoft.EntityFrameworkCore;
using Previsio.Api.Excecoes;
using Previsio.Api.Servicos;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Data;

namespace Previsio.Api.Jobs;

/// <summary>
/// Transforma as notas registradas em contas a pagar no Bling:
/// garante o fornecedor (contato) e cria uma conta por parcela.
/// </summary>
public class LancamentoNotaJob
{
    private const int MaximoTentativas = 5;
    private const int LoteMaximo = 100;

    private readonly PrevisioDbContext _db;
    private readonly BlingClient _bling;
    private readonly TokenBlingService _tokens;
    private readonly ILogger<LancamentoNotaJob> _logger;

    public LancamentoNotaJob(
        PrevisioDbContext db,
        BlingClient bling,
        TokenBlingService tokens,
        ILogger<LancamentoNotaJob> logger)
    {
        _db = db;
        _bling = bling;
        _tokens = tokens;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 0)]
    public async Task LancarPendentesAsync(CancellationToken cancelamento)
    {
        List<int> ids = await _db.NotasFiscais
            .Where(n => n.Status == StatusNota.Recebida
                || n.Status == StatusNota.BlingDesconectado
                || (n.Status == StatusNota.Erro && n.Tentativas < MaximoTentativas))
            .OrderBy(n => n.RecebidaEm)
            .Select(n => n.Id)
            .Take(LoteMaximo)
            .ToListAsync(cancelamento);

        foreach (int id in ids)
        {
            bool blingRespondeu = await LancarAsync(id, cancelamento);
            if (!blingRespondeu)
            {
                // Sem conexão com o Bling: as próximas notas falhariam do mesmo jeito.
                // Para aqui e tenta tudo de novo na próxima execução.
                _logger.LogWarning("Lançamento interrompido: sem conexão com o Bling.");
                break;
            }
        }
    }

    /// <summary>
    /// Lança a nota no Bling. Retorna false só quando o Bling não respondeu
    /// (rede ou tempo esgotado), para quem chama poder parar o lote.
    /// </summary>
    public async Task<bool> LancarAsync(int notaId, CancellationToken cancelamento)
    {
        NotaFiscal? nota = await _db.NotasFiscais
            .Include(n => n.Parcelas)
            .Include(n => n.Empresa)
            .FirstOrDefaultAsync(n => n.Id == notaId, cancelamento);

        if (nota == null || nota.Empresa == null)
        {
            return true;
        }
        if (nota.Status == StatusNota.Lancada)
        {
            return true;
        }

        Empresa empresa = nota.Empresa;
        string conta = TokenBlingService.IdentificadorConta(empresa);

        // Etapa e parcela em andamento, para o registro de falha dizer onde parou
        EtapaFalha etapa = EtapaFalha.RenovacaoToken;
        ParcelaNota? parcelaAtual = null;

        try
        {
            string token = await _tokens.ObterAccessTokenAsync(empresa, cancelamento);

            etapa = EtapaFalha.BuscaFornecedor;
            long contatoId;
            ContatoBling? existente = await _bling.BuscarContatoPorDocumentoAsync(
                conta, token, nota.CnpjFornecedor, cancelamento);

            if (existente != null)
            {
                contatoId = existente.Id;
            }
            else
            {
                etapa = EtapaFalha.CadastroFornecedor;
                contatoId = await _bling.CriarContatoAsync(conta, token, MontarFornecedor(nota), cancelamento);
            }

            etapa = EtapaFalha.LancamentoContaPagar;
            int totalParcelas = nota.Parcelas.Count;
            foreach (ParcelaNota parcela in nota.Parcelas.OrderBy(p => p.Vencimento))
            {
                // Em caso de retentativa, não recria parcelas que já foram lançadas
                if (parcela.BlingContaPagarId.HasValue)
                {
                    continue;
                }

                parcelaAtual = parcela;
                NovaContaPagarBling novaConta = MontarContaPagar(nota, parcela, totalParcelas, contatoId);
                parcela.BlingContaPagarId = await _bling.CriarContaPagarAsync(conta, token, novaConta, cancelamento);

                // Salva parcela a parcela: se cair no meio, a próxima tentativa continua de onde parou
                await _db.SaveChangesAsync(cancelamento);
            }

            nota.Status = StatusNota.Lancada;
            nota.MensagemErro = null;
            nota.LancadaEm = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancelamento);

            _logger.LogInformation("NF {Numero} ({Chave}) lançada no Bling da empresa {Empresa}",
                nota.Numero, nota.ChaveAcesso, empresa.Nome);
            return true;
        }
        catch (EmpresaDesconectadaException erro)
        {
            nota.Status = StatusNota.BlingDesconectado;
            nota.MensagemErro = erro.Message;

            // Só registra falha quando a renovação do token foi recusada pelo Bling.
            // Empresa que simplesmente não está conectada não é falha: a nota fica aguardando.
            BlingException? causa = erro.InnerException as BlingException;
            if (causa != null)
            {
                _db.Falhas.Add(FabricaFalha.DoBling(EtapaFalha.RenovacaoToken, causa, nota, null));
            }

            await _db.SaveChangesAsync(cancelamento);
            return true;
        }
        catch (BlingException erro)
        {
            nota.Status = StatusNota.Erro;
            nota.MensagemErro = MontarMensagemErro(erro);

            // Queda de conexão não é culpa da nota: não gasta uma das tentativas
            if (!erro.FalhaDeConexao)
            {
                nota.Tentativas = nota.Tentativas + 1;
            }

            _db.Falhas.Add(FabricaFalha.DoBling(etapa, erro, nota, parcelaAtual));
            await _db.SaveChangesAsync(cancelamento);

            _logger.LogWarning(erro, "Falha ao lançar a NF {Chave} na etapa {Etapa} ({Metodo} {Endereco}). Resposta do Bling: {Corpo}",
                nota.ChaveAcesso, etapa, erro.Metodo, erro.Endereco, erro.CorpoResposta);

            if (erro.FalhaDeConexao)
            {
                return false;
            }
            return true;
        }
    }

    private static NovoContatoBling MontarFornecedor(NotaFiscal nota)
    {
        var novo = new NovoContatoBling();
        novo.Nome = nota.NomeFornecedor;
        novo.NumeroDocumento = nota.CnpjFornecedor;
        if (nota.CnpjFornecedor.Length == 11)
        {
            novo.Tipo = "F";
        }
        else
        {
            novo.Tipo = "J";
        }
        return novo;
    }

    private static NovaContaPagarBling MontarContaPagar(
        NotaFiscal nota, ParcelaNota parcela, int totalParcelas, long contatoId)
    {
        var conta = new NovaContaPagarBling();
        conta.Contato = new Referencia { Id = contatoId };
        conta.Valor = parcela.Valor;
        conta.Vencimento = BlingClient.FormatarData(parcela.Vencimento);
        conta.DataEmissao = BlingClient.FormatarData(nota.DataEmissao);
        conta.Competencia = BlingClient.FormatarData(nota.DataEmissao);

        // Ex.: "12345/2" = nota 12345, segunda parcela
        if (totalParcelas > 1)
        {
            conta.NumeroDocumento = nota.Numero + "/" + LimparNumeroParcela(parcela.NumeroParcela);
        }
        else
        {
            conta.NumeroDocumento = nota.Numero;
        }

        // O Bling não tem campo para a chave da NF-e; ela vai no histórico para rastreio
        conta.Historico = "NF-e " + nota.Numero + " - " + nota.NomeFornecedor
            + " | Chave " + nota.ChaveAcesso
            + " | Lançado pelo Previsio";

        return conta;
    }

    private static string LimparNumeroParcela(string numero)
    {
        string limpo = numero.TrimStart('0');
        if (limpo == "")
        {
            return numero;
        }
        return limpo;
    }

    private static string MontarMensagemErro(BlingException erro)
    {
        string mensagem = erro.Message;
        if (!string.IsNullOrEmpty(erro.CorpoResposta))
        {
            mensagem = mensagem + " | " + erro.CorpoResposta;
        }
        if (mensagem.Length > 2000)
        {
            mensagem = mensagem.Substring(0, 2000);
        }
        return mensagem;
    }
}
