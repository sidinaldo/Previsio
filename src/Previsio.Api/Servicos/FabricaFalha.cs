using Previsio.Bling;
using Previsio.Core.Entidades;

namespace Previsio.Api.Servicos;

/// <summary>
/// Monta os registros de FalhaProcessamento com os dados da nota copiados
/// e os textos cortados no tamanho das colunas.
/// </summary>
public static class FabricaFalha
{
    public const int TamanhoMensagem = 2000;
    public const int TamanhoEndereco = 500;
    public const int TamanhoResposta = 4000;
    public const int TamanhoOrigem = 300;

    public const string MetodoImap = "IMAP";

    /// <summary>
    /// Falha numa chamada ao Bling durante o lançamento de uma nota.
    /// </summary>
    public static FalhaProcessamento DoBling(
        EtapaFalha etapa, BlingException erro, NotaFiscal nota, ParcelaNota? parcela)
    {
        FalhaProcessamento falha = DaNota(etapa, erro.Message, nota);
        PreencherChamadaBling(falha, erro);

        if (parcela != null)
        {
            falha.NumeroParcela = Cortar(parcela.NumeroParcela, 20);
            falha.ValorParcela = parcela.Valor;
            falha.VencimentoParcela = parcela.Vencimento;
        }
        return falha;
    }

    /// <summary>
    /// Falha ao renovar o token de uma empresa fora do lançamento de uma nota
    /// (ex.: na sincronização das contas a pagar).
    /// </summary>
    public static FalhaProcessamento DaEmpresa(EtapaFalha etapa, BlingException erro, Empresa empresa)
    {
        var falha = new FalhaProcessamento();
        falha.ClienteId = empresa.ClienteId;
        falha.EmpresaId = empresa.Id;
        falha.Etapa = etapa;
        falha.Mensagem = Cortar(erro.Message, TamanhoMensagem);
        PreencherChamadaBling(falha, erro);
        return falha;
    }

    /// <summary>
    /// Falha ao conectar ou ler a caixa de e-mail do cliente.
    /// </summary>
    public static FalhaProcessamento DoEmail(
        int clienteId, int? empresaId, ICaixaEmail caixa, string mensagem, string? origem)
    {
        var falha = new FalhaProcessamento();
        falha.ClienteId = clienteId;
        falha.EmpresaId = empresaId;
        falha.Etapa = EtapaFalha.LeituraEmail;
        falha.Mensagem = Cortar(mensagem, TamanhoMensagem);
        falha.Metodo = MetodoImap;
        falha.Endereco = Cortar(EnderecoImap(caixa), TamanhoEndereco);
        if (origem != null)
        {
            falha.Origem = Cortar(origem, TamanhoOrigem);
        }
        return falha;
    }

    /// <summary>
    /// Anexo .xml que não foi reconhecido como NF-e.
    /// </summary>
    public static FalhaProcessamento DoXml(
        int clienteId, int? empresaId, ICaixaEmail caixa, string nomeAnexo, string origemEmail)
    {
        string mensagem = "O anexo \"" + nomeAnexo + "\" não foi reconhecido como NF-e (modelo 55). "
            + "Pode ser um evento (cancelamento, carta de correção), CT-e, NFS-e ou um arquivo corrompido.";

        FalhaProcessamento falha = DoEmail(clienteId, empresaId, caixa, mensagem, origemEmail);
        falha.Etapa = EtapaFalha.LeituraXml;
        return falha;
    }

    public static string EnderecoImap(ICaixaEmail caixa)
    {
        string protocolo = "imap://";
        if (caixa.ImapSsl)
        {
            protocolo = "imaps://";
        }
        return protocolo + caixa.ImapHost + ":" + caixa.ImapPorta + " (usuário " + caixa.ImapUsuario + ")";
    }

    public static string Cortar(string texto, int tamanho)
    {
        if (texto.Length <= tamanho)
        {
            return texto;
        }
        return texto.Substring(0, tamanho);
    }

    private static FalhaProcessamento DaNota(EtapaFalha etapa, string mensagem, NotaFiscal nota)
    {
        var falha = new FalhaProcessamento();
        falha.ClienteId = nota.ClienteId;
        falha.EmpresaId = nota.EmpresaId;
        falha.NotaFiscalId = nota.Id;
        falha.Etapa = etapa;
        falha.Mensagem = Cortar(mensagem, TamanhoMensagem);
        falha.NumeroNota = nota.Numero;
        falha.ChaveAcesso = nota.ChaveAcesso;
        falha.Fornecedor = Cortar(nota.NomeFornecedor, 200);
        falha.CnpjFornecedor = nota.CnpjFornecedor;
        falha.ValorNota = nota.ValorTotal;
        falha.Origem = Cortar(nota.OrigemEmail, TamanhoOrigem);
        return falha;
    }

    private static void PreencherChamadaBling(FalhaProcessamento falha, BlingException erro)
    {
        falha.Metodo = Cortar(erro.Metodo, 10);
        falha.Endereco = Cortar(erro.Endereco, TamanhoEndereco);

        if (erro.FalhaDeConexao)
        {
            falha.StatusHttp = null;
        }
        else
        {
            falha.StatusHttp = (int)erro.StatusCode;
        }

        if (!string.IsNullOrEmpty(erro.CorpoResposta))
        {
            falha.Resposta = Cortar(erro.CorpoResposta, TamanhoResposta);
        }
    }
}
