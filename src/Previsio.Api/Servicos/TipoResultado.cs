namespace Previsio.Api.Servicos;

/// <summary>
/// Como terminou uma operação de serviço. O controller traduz em resposta HTTP.
/// </summary>
public enum TipoResultado
{
    Sucesso,
    NaoEncontrado,
    Invalido,
    Conflito
}
