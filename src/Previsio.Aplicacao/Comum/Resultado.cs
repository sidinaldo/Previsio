namespace Previsio.Aplicacao.Comum;

/// <summary>
/// Resultado de uma operação de serviço que não devolve dados.
/// </summary>
public class Resultado
{
    public TipoResultado Tipo { get; private set; }

    // Mensagem para quem chamou a API (nula quando não há o que dizer)
    public string? Mensagem { get; private set; }

    public bool Sucesso
    {
        get { return Tipo == TipoResultado.Sucesso; }
    }

    private Resultado(TipoResultado tipo, string? mensagem)
    {
        Tipo = tipo;
        Mensagem = mensagem;
    }

    public static Resultado Ok()
    {
        return new Resultado(TipoResultado.Sucesso, null);
    }

    public static Resultado NaoEncontrado(string? mensagem)
    {
        return new Resultado(TipoResultado.NaoEncontrado, mensagem);
    }

    public static Resultado Invalido(string mensagem)
    {
        return new Resultado(TipoResultado.Invalido, mensagem);
    }

    public static Resultado Conflito(string mensagem)
    {
        return new Resultado(TipoResultado.Conflito, mensagem);
    }
}
