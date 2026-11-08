using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

/// <summary>
/// Translates domain and framework exceptions into messages a store employee
/// can act on. The domain keeps its English messages for code and tests.
/// </summary>
public static class DomainErrorTranslator
{
    public static string ToSpanish(Exception exception) => exception switch
    {
        DuplicateSkuException =>
            "Ya existe un producto con ese SKU. Usa un código diferente.",
        DuplicateBarcodeException =>
            "Ya existe un producto con ese código de barras.",
        InvalidProductException =>
            "Revisa los datos del producto. El SKU, el código de barras y el nombre son obligatorios, y el precio debe ser mayor que cero.",
        ProductNotFoundException =>
            "No encontramos el producto solicitado. Verifica el SKU.",
        StockNotFoundException =>
            "No hay stock registrado para ese producto en la tienda seleccionada.",
        InsufficientStockException =>
            "No hay unidades suficientes para completar la operación.",
        ArgumentOutOfRangeException =>
            "La cantidad debe ser mayor que cero.",
        _ => "Ocurrió un error inesperado. Intenta de nuevo."
    };
}
