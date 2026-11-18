namespace Previsio.Aplicacao.Abstracoes.Erp;

/// <summary>
/// O que o Previsio precisa do ERP de uma empresa: autorização, fornecedores, contas a pagar e a receber,
/// categorias e formas de pagamento. Hoje só o Bling implementa (Previsio.Bling).
///
/// "conta" identifica a conta do ERP para o limite de requisições (uma por empresa) e "accessToken"
/// é o token daquela conta. Erros de comunicação saem como <see cref="ErpException"/>.
/// </summary>
public interface IErp
{
    /// <summary>
    /// Aplicativo global do Previsio, configurado no appsettings.
    /// </summary>
    CredenciaisErp CredenciaisPadrao { get; }

    string MontarUrlAutorizacao(CredenciaisErp credenciais, string estado);

    Task<TokenErp> TrocarCodigoPorTokenAsync(CredenciaisErp credenciais, string codigo, CancellationToken cancelamento);

    Task<TokenErp> RenovarTokenAsync(CredenciaisErp credenciais, string refreshToken, CancellationToken cancelamento);

    Task<ContatoErp?> BuscarContatoPorDocumentoAsync(
        string conta, string accessToken, string documento, CancellationToken cancelamento);

    Task<long> CriarContatoAsync(string conta, string accessToken, NovoContatoErp contato, CancellationToken cancelamento);

    /// <summary>
    /// Contato pelo id (a lista de contas a pagar só traz o id do fornecedor).
    /// </summary>
    Task<ContatoErp?> ObterContatoAsync(string conta, string accessToken, long id, CancellationToken cancelamento);

    Task<long> CriarContaPagarAsync(
        string conta, string accessToken, NovaContaPagarErp contaPagar, CancellationToken cancelamento);

    /// <summary>
    /// Contas a pagar com vencimento no período, em todas as situações.
    /// </summary>
    Task<List<ContaPagarErp>> ListarContasPagarAsync(
        string conta, string accessToken, DateTime vencimentoInicial, DateTime vencimentoFinal, CancellationToken cancelamento);

    /// <summary>
    /// Detalhe de uma conta a pagar (traz o número do documento, o histórico e a categoria).
    /// </summary>
    Task<ContaPagarErp?> ObterContaPagarAsync(string conta, string accessToken, long id, CancellationToken cancelamento);

    /// <summary>
    /// Contas a receber com vencimento no período. Quem chama confere a data de novo.
    /// </summary>
    Task<List<ContaReceberErp>> ListarContasReceberAsync(
        string conta, string accessToken, DateTime vencimentoInicial, DateTime vencimentoFinal, CancellationToken cancelamento);

    Task<List<CategoriaErp>> ListarCategoriasAsync(string conta, string accessToken, CancellationToken cancelamento);

    Task<List<FormaPagamentoErp>> ListarFormasPagamentoAsync(string conta, string accessToken, CancellationToken cancelamento);
}
