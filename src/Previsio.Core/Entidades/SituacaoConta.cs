namespace Previsio.Core.Entidades;

/// <summary>
/// Situações de conta a pagar e a receber. Os números são os da API v3 do Bling; a integração
/// de outro ERP converte as situações dele para estes.
/// </summary>
public static class SituacaoConta
{
    public const int EmAberto = 1;
    public const int Pago = 2;
    public const int ParcialmentePago = 3;
    public const int Devolvido = 4;
    public const int Cancelado = 5;
}
