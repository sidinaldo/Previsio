namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Seção "Formas de pagamento" da rede: forma padrão, regras por fornecedor e o que falta em cada empresa.
/// </summary>
public class ConfiguracaoFormasPagamentoDto
{
    // Nome da forma das notas sem regra, sem histórico e sem tipo útil no XML (nulo = usa a automática)
    public string? FormaPadrao { get; set; }
    // Forma usada quando a rede não escolhe uma (appsettings FormasPagamento:PadraoAutomatico; nulo = desligada)
    public string? PadraoAutomatico { get; set; }
    // Empresas cujo Bling não tem a forma padrão em uso (a escolhida ou a automática)
    public List<string> PadraoAusenteEm { get; set; } = new List<string>();
    // Formas ativas no Bling de alguma empresa da rede, com o grupo
    public List<OpcaoFormaPagamentoDto> Opcoes { get; set; } = new List<OpcaoFormaPagamentoDto>();
    public List<RegraFormaPagamentoDto> Regras { get; set; } = new List<RegraFormaPagamentoDto>();
    public List<FornecedorDaRedeDto> Fornecedores { get; set; } = new List<FornecedorDaRedeDto>();
    // Empresas conectadas sem nenhuma forma guardada (sem o escopo ou ainda não sincronizadas)
    public List<string> EmpresasSemFormas { get; set; } = new List<string>();
    // Nomes dos grupos que saem sozinhos da conta (appsettings FormasPagamento:GruposSaiSozinho)
    public List<string> GruposSaiSozinho { get; set; } = new List<string>();
}
