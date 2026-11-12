namespace Previsio.Aplicacao.Bling;

public class EmpresaDesconectadaException : Exception
{
    public EmpresaDesconectadaException(string mensagem) : base(mensagem)
    {
    }

    /// <summary>
    /// Usado quando a conexão caiu porque a renovação do token falhou;
    /// o erro original (com o endereço chamado) fica em InnerException.
    /// </summary>
    public EmpresaDesconectadaException(string mensagem, Exception erroOriginal) : base(mensagem, erroOriginal)
    {
    }
}
