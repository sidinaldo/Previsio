namespace Previsio.Api.Servicos;

/// <summary>
/// Resultado de uma operação de serviço que devolve um valor quando dá certo.
/// </summary>
public class Resultado<T>
{
    public TipoResultado Tipo { get; private set; }
    public T? Valor { get; private set; }

    // Mensagem para quem chamou a API (nula quando não há o que dizer)
    public string? Mensagem { get; private set; }

    public bool Sucesso
    {
        get { return Tipo == TipoResultado.Sucesso; }
    }

    private Resultado(TipoResultado tipo, T? valor, string? mensagem)
    {
        Tipo = tipo;
        Valor = valor;
        Mensagem = mensagem;
    }

    public static Resultado<T> Ok(T valor)
    {
        return new Resultado<T>(TipoResultado.Sucesso, valor, null);
    }

    public static Resultado<T> NaoEncontrado(string? mensagem)
    {
        return new Resultado<T>(TipoResultado.NaoEncontrado, default, mensagem);
    }

    public static Resultado<T> Invalido(string mensagem)
    {
        return new Resultado<T>(TipoResultado.Invalido, default, mensagem);
    }

    public static Resultado<T> Conflito(string mensagem)
    {
        return new Resultado<T>(TipoResultado.Conflito, default, mensagem);
    }
}
