namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Card "Categorias" da rede: categoria padrão, regras por fornecedor e o que falta em cada empresa.
/// </summary>
public class ConfiguracaoCategoriasDto
{
    // Nome da categoria das notas sem regra e sem histórico no Bling (nulo = usa a automática)
    public string? CategoriaPadrao { get; set; }
    // Categoria usada quando a rede não escolhe uma (appsettings Categorias:PadraoAutomatico; nulo = desligada)
    public string? PadraoAutomatico { get; set; }
    // Empresas cujo Bling não tem a categoria padrão em uso (a escolhida ou a automática)
    public List<string> PadraoAusenteEm { get; set; } = new List<string>();
    // Categorias de despesa que existem no Bling de alguma empresa da rede
    public List<string> Opcoes { get; set; } = new List<string>();
    public List<RegraCategoriaDto> Regras { get; set; } = new List<RegraCategoriaDto>();
    // Fornecedores das notas já recebidas, para escolher na regra
    public List<FornecedorDaRedeDto> Fornecedores { get; set; } = new List<FornecedorDaRedeDto>();
    // Empresas conectadas sem nenhuma categoria guardada (sem o escopo de categorias ou ainda não sincronizadas)
    public List<string> EmpresasSemCategorias { get; set; } = new List<string>();
}
