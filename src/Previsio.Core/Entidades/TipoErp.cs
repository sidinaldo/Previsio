namespace Previsio.Core.Entidades;

/// <summary>
/// ERP em que a empresa lança as contas a pagar. Cada empresa (CNPJ) usa um só.
/// Guardado como número no banco: não reaproveite um valor ao remover um ERP.
/// </summary>
public enum TipoErp
{
    Bling = 1
}
