namespace Previsio.Core.Entidades;

/// <summary>
/// Situações de conta a pagar conforme a API v3 do Bling.
/// </summary>
public static class SituacaoContaBling
{
    public const int EmAberto = 1;
    public const int Pago = 2;
    public const int ParcialmentePago = 3;
    public const int Devolvido = 4;
    public const int Cancelado = 5;
}
