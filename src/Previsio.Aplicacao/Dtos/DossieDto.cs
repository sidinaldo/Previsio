namespace Previsio.Aplicacao.Dtos;

/// <summary>
/// Dossiê financeiro da rede: projeção de caixa, saúde dos lançamentos e comparativo por empresa,
/// fornecedor e categoria. É a tela de Relatórios e o documento impresso.
/// </summary>
public class DossieDto
{
    public string RedeNome { get; set; } = "";
    public List<EmpresaDossieDto> Empresas { get; set; } = new List<EmpresaDossieDto>();
    public DateTime GeradoEm { get; set; }
    // Horizonte da projeção (30, 60 ou 90 dias)
    public int Dias { get; set; }
    // Período do comparativo e da auditoria
    public DateTime De { get; set; }
    public DateTime Ate { get; set; }

    public ProjecaoCaixaDto Projecao { get; set; } = new ProjecaoCaixaDto();
    public AuditoriaLancamentosDto Auditoria { get; set; } = new AuditoriaLancamentosDto();
    public ComparativoDto Comparativo { get; set; } = new ComparativoDto();
}
