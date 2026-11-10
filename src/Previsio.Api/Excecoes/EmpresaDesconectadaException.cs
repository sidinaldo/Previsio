namespace Previsio.Api.Excecoes;

public class EmpresaDesconectadaException : Exception
{
    public EmpresaDesconectadaException(string mensagem) : base(mensagem)
    {
    }
}
