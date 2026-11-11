namespace Previsio.Api.Servicos;

/// <summary>
/// Como terminou a volta do OAuth do Bling. O valor vai na URL da tela de empresas (?bling=...).
/// </summary>
public static class RetornoConexaoBling
{
    public const string Conectado = "conectado";
    public const string Expirado = "expirado";
    public const string Erro = "erro";
}
