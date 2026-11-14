namespace Previsio.Aplicacao.Relatorios;

/// <summary>
/// Seção "Relatorios" do appsettings: regras do Dossiê financeiro.
/// </summary>
public class RelatoriosOptions
{
    public const string Secao = "Relatorios";

    public const decimal FatorSemanaCriticaPadrao = 1.5m;
    public const int TamanhoRankingPadrao = 10;
    public const int ConformidadeVerdePadrao = 90;
    public const int ConformidadeAmarelaPadrao = 70;

    // Semana crítica (pico de desembolso): saídas a partir de X vezes a média semanal do horizonte
    public decimal FatorSemanaCritica { get; set; } = FatorSemanaCriticaPadrao;

    // Quantos fornecedores e categorias entram no ranking
    public int TamanhoRanking { get; set; } = TamanhoRankingPadrao;

    // Faixas do índice de conformidade (0 a 100): a partir de Verde é verde, de Amarela é amarelo, abaixo é vermelho
    public int ConformidadeVerde { get; set; } = ConformidadeVerdePadrao;
    public int ConformidadeAmarela { get; set; } = ConformidadeAmarelaPadrao;

    // Peso de cada indicador no índice (0 tira o indicador do índice)
    public int PesoCategorias { get; set; } = 1;
    public int PesoNotasLancadas { get; set; } = 1;
    public int PesoContasEmDia { get; set; } = 1;

    public decimal FatorSemanaCriticaValido()
    {
        if (FatorSemanaCritica < 1m || FatorSemanaCritica > 10m)
        {
            return FatorSemanaCriticaPadrao;
        }
        return FatorSemanaCritica;
    }

    public int TamanhoRankingValido()
    {
        if (TamanhoRanking < 3 || TamanhoRanking > 50)
        {
            return TamanhoRankingPadrao;
        }
        return TamanhoRanking;
    }

    /// <summary>As duas faixas, com a verde acima da amarela; fora disso, as padrão.</summary>
    public int ConformidadeVerdeValida()
    {
        if (!FaixasValidas())
        {
            return ConformidadeVerdePadrao;
        }
        return ConformidadeVerde;
    }

    public int ConformidadeAmarelaValida()
    {
        if (!FaixasValidas())
        {
            return ConformidadeAmarelaPadrao;
        }
        return ConformidadeAmarela;
    }

    private bool FaixasValidas()
    {
        return ConformidadeAmarela >= 0 && ConformidadeVerde <= 100 && ConformidadeAmarela < ConformidadeVerde;
    }

    public static int PesoValido(int peso)
    {
        if (peso < 0 || peso > 10)
        {
            return 1;
        }
        return peso;
    }
}
