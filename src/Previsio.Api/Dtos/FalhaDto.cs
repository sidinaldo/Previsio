namespace Previsio.Api.Dtos;

public class FalhaDto
{
    public int Id { get; set; }
    public DateTime OcorridaEm { get; set; }

    // Nome da etapa (LeituraEmail, LeituraXml, RenovacaoToken, BuscaFornecedor,
    // CadastroFornecedor, LancamentoContaPagar)
    public string Etapa { get; set; } = "";

    public string Mensagem { get; set; } = "";
    public string Metodo { get; set; } = "";
    public string Endereco { get; set; } = "";
    public int? StatusHttp { get; set; }
    public string? Resposta { get; set; }

    public int? EmpresaId { get; set; }
    public string? EmpresaNome { get; set; }

    public int? NotaFiscalId { get; set; }
    public string? NumeroNota { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Fornecedor { get; set; }
    public string? CnpjFornecedor { get; set; }
    public decimal? ValorNota { get; set; }

    public string? NumeroParcela { get; set; }
    public decimal? ValorParcela { get; set; }
    public DateTime? VencimentoParcela { get; set; }

    public string? Origem { get; set; }
}
