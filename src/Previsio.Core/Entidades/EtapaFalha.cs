namespace Previsio.Core.Entidades;

/// <summary>
/// Em que ponto do caminho "e-mail → Bling" a nota falhou.
/// </summary>
public enum EtapaFalha
{
    // Conectar, autenticar ou ler uma mensagem da caixa de e-mail (IMAP)
    LeituraEmail = 0,

    // Anexo .xml que não foi reconhecido como NF-e
    LeituraXml = 1,

    // Renovar o acesso da empresa ao Bling (refresh token)
    RenovacaoToken = 2,

    // Procurar o fornecedor no Bling pelo CNPJ (GET /contatos)
    BuscaFornecedor = 3,

    // Cadastrar o fornecedor no Bling (POST /contatos)
    CadastroFornecedor = 4,

    // Criar a conta a pagar no Bling (POST /contas/pagar)
    LancamentoContaPagar = 5,

    // Conferir no Bling se a nota já foi lançada à mão (GET /contas/pagar)
    ConferenciaBling = 6
}
