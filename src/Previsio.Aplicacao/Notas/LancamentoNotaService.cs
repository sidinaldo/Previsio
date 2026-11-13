using Microsoft.EntityFrameworkCore;
using Previsio.Bling;
using Previsio.Bling.Modelos;
using Previsio.Core.Entidades;
using Previsio.Core.Abstracoes;
using Previsio.Core.Contratos;
using Previsio.Core.Conferencia;
using Previsio.Aplicacao.Falhas;
using Previsio.Aplicacao.Bling;
using Previsio.Aplicacao.Conferencia;

namespace Previsio.Aplicacao.Notas;

/// <summary>
/// Transforma as notas registradas em contas a pagar no Bling:
/// garante o fornecedor (contato) e cria uma conta por parcela.
/// </summary>
public class LancamentoNotaService
{
    private const int MaximoTentativas = RegrasLancamento.MaximoTentativas;
    private const int LoteMaximo = 100;

    private readonly IRepositorio<NotaFiscal> _notas;
    private readonly IRepositorio<Cliente> _clientes;
    private readonly IRepositorio<FalhaProcessamento> _falhas;
    private readonly IRepositorio<ContaPagarSincronizada> _contas;
    private readonly BlingClient _bling;
    private readonly TokenBlingService _tokens;
    private readonly ConferenciaBlingService _conferencia;
    private readonly ILogger<LancamentoNotaService> _logger;

    public LancamentoNotaService(
        IRepositorio<NotaFiscal> notas,
        IRepositorio<Cliente> clientes,
        IRepositorio<FalhaProcessamento> falhas,
        IRepositorio<ContaPagarSincronizada> contas,
        BlingClient bling,
        TokenBlingService tokens,
        ConferenciaBlingService conferencia,
        ILogger<LancamentoNotaService> logger)
    {
        _notas = notas;
        _clientes = clientes;
        _falhas = falhas;
        _contas = contas;
        _bling = bling;
        _tokens = tokens;
        _conferencia = conferencia;
        _logger = logger;
    }

    public async Task LancarPendentesAsync(CancellationToken cancelamento)
    {
        // Só das redes ativas e contratadas ou com o teste grátis no prazo
        List<int> redes = await _clientes.Consultar()
            .Where(PeriodoTeste.Processa(DateTime.UtcNow))
            .Select(c => c.Id)
            .ToListAsync(cancelamento);

        List<int> ids = await _notas.Consultar()
            .Where(n => redes.Contains(n.ClienteId)
                && (n.Status == StatusNota.Recebida
                    || n.Status == StatusNota.BlingDesconectado
                    || (n.Status == StatusNota.Erro && n.Tentativas < MaximoTentativas)))
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
        NotaFiscal? nota = await _notas.Consultar()
            .Include(n => n.Parcelas)
            .Include(n => n.Empresa)
            .FirstOrDefaultAsync(n => n.Id == notaId, cancelamento);

        if (nota == null || nota.Empresa == null)
        {
            return true;
        }
        // Rede desativada ou com o teste grátis acabado: não lança (nem pelo "tentar de novo")
        int clienteId = nota.ClienteId;
        bool processa = await _clientes.Consultar().Where(c => c.Id == clienteId).AnyAsync(PeriodoTeste.Processa(DateTime.UtcNow), cancelamento);
        if (!processa)
        {
            return true;
        }
        // Já lançada (pelo Previsio ou manualmente no Bling): não lança de novo
        if (nota.Status == StatusNota.Lancada || nota.Status == StatusNota.LancadaManualmente)
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
            ContatoBling? existente;
            try
            {
                existente = await _bling.BuscarContatoPorDocumentoAsync(conta, token, nota.CnpjFornecedor, cancelamento);
            }
            catch (BlingException erro)
            {
                // Primeira chamada da nota: se o Bling revogou o token, renova uma vez e repete
                if (!erro.TokenInvalido)
                {
                    throw;
                }
                etapa = EtapaFalha.RenovacaoToken;
                token = await _tokens.RenovarAposRecusaAsync(empresa, cancelamento);
                etapa = EtapaFalha.BuscaFornecedor;
                existente = await _bling.BuscarContatoPorDocumentoAsync(conta, token, nota.CnpjFornecedor, cancelamento);
            }

            if (existente != null)
            {
                contatoId = existente.Id;
            }
            else
            {
                etapa = EtapaFalha.CadastroFornecedor;
                contatoId = await _bling.CriarContatoAsync(conta, token, MontarFornecedor(nota), cancelamento);
            }

            // Conferência com o Bling: alguém pode ter lançado esta nota à mão antes do Previsio
            bool nenhumaLancada = !nota.Parcelas.Any(p => p.BlingContaPagarId.HasValue);
            if (existente != null && nenhumaLancada)
            {
                Cliente? rede = await _clientes.ConsultarSomenteLeitura().FirstOrDefaultAsync(c => c.Id == nota.ClienteId, cancelamento);
                if (rede != null && rede.ConferirBlingAntesDeLancar)
                {
                    etapa = EtapaFalha.ConferenciaBling;
                    ResultadoConferencia resultado = await _conferencia.ConferirNoBlingAsync(nota, rede, conta, token, contatoId, cancelamento);
                    if (resultado.Completa)
                    {
                        ConferenciaBlingService.MarcarEncontrada(nota, resultado);
                        foreach (ParcelaNota encontrada in nota.Parcelas)
                        {
                            await GravarContaNoPrevisioAsync(nota, empresa.Id, encontrada, contatoId, cancelamento);
                        }
                        await _notas.SalvarAsync(cancelamento);
                        _logger.LogInformation("NF {Numero} já estava lançada no Bling (contas {Contas}): não lançada de novo",
                            nota.Numero, resultado.Contas());
                        return true;
                    }
                    if (resultado.Parcial)
                    {
                        // Encontro parcial nunca lança sozinho: fica para alguém conferir (e vira aviso)
                        nota.Status = StatusNota.Erro;
                        nota.MensagemErro = ConferenciaBlingService.MensagemParcial(resultado);
                        nota.Tentativas = MaximoTentativas;
                        await _notas.SalvarAsync(cancelamento);
                        return true;
                    }
                }
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
                await GravarContaNoPrevisioAsync(nota, empresa.Id, parcela, contatoId, cancelamento);

                // Salva parcela a parcela: se cair no meio, a próxima tentativa continua de onde parou
                await _notas.SalvarAsync(cancelamento);
            }

            nota.Status = StatusNota.Lancada;
            nota.MensagemErro = null;
            nota.LancadaEm = DateTime.UtcNow;
            await _notas.SalvarAsync(cancelamento);

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
                _falhas.Adicionar(FabricaFalha.DoBling(EtapaFalha.RenovacaoToken, causa, nota, null));
            }

            await _notas.SalvarAsync(cancelamento);
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

            _falhas.Adicionar(FabricaFalha.DoBling(etapa, erro, nota, parcelaAtual));
            await _notas.SalvarAsync(cancelamento);

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

    /// <summary>
    /// Grava na hora a conta a pagar que acabou de ir para o Bling, com os dados da parcela.
    /// Sem isso a nota sumia do painel até a sincronização seguinte (saía de "fora do Bling"
    /// e ainda não estava na previsão). A sincronização depois confere com o Bling e corrige.
    /// </summary>
    private async Task GravarContaNoPrevisioAsync(
        NotaFiscal nota, int empresaId, ParcelaNota parcela, long contatoId, CancellationToken cancelamento)
    {
        if (!parcela.BlingContaPagarId.HasValue)
        {
            return;
        }

        long blingId = parcela.BlingContaPagarId.Value;
        // Conta já trazida pela sincronização (por exemplo, a lançada à mão que a conferência achou)
        bool jaExiste = await _contas.ExisteAsync(c => c.EmpresaId == empresaId && c.BlingId == blingId, cancelamento);
        if (jaExiste)
        {
            return;
        }

        var contaLocal = new ContaPagarSincronizada();
        contaLocal.EmpresaId = empresaId;
        contaLocal.BlingId = blingId;
        contaLocal.Vencimento = parcela.Vencimento;
        contaLocal.Valor = parcela.Valor;
        contaLocal.Situacao = SituacaoContaBling.EmAberto;
        contaLocal.BlingContatoId = contatoId;
        contaLocal.FornecedorNome = nota.NomeFornecedor;
        _contas.Adicionar(contaLocal);
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
