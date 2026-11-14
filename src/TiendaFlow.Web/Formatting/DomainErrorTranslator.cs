using Microsoft.Extensions.Logging;
using TiendaFlow.Application.Payroll;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

/// <summary>
/// Translates domain and framework exceptions into messages a store employee
/// can act on. The domain keeps its English messages for code and tests.
/// Unexpected exceptions are logged before the generic message is returned:
/// the user never sees technical detail, but the server log keeps the type,
/// message and stack trace.
/// </summary>
public static class DomainErrorTranslator
{
    private const string UnexpectedMessage =
        "Ocurrió un error inesperado. Intenta de nuevo.";

    private static ILogger? _logger;

    /// <summary>
    /// Wires the application logger once at startup. Pages call the static
    /// translator from every catch block; centralizing the log here avoids
    /// repeating the same try/log code in more than sixty call sites.
    /// </summary>
    public static void UseLogger(ILogger logger) => _logger = logger;

    public static string ToSpanish(Exception exception)
    {
        var message = Translate(exception);

        if (message == UnexpectedMessage)
        {
            _logger?.LogError(
                exception,
                "Excepción no controlada en la interfaz: {ExceptionType}",
                exception.GetType().FullName);
        }

        return message;
    }

    private static string Translate(Exception exception) => exception switch
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
        AccessDeniedException =>
            "Tu rol no tiene permiso para realizar esta operación.",
        InvalidUserException =>
            "Revisa los datos del usuario. El correo, el documento y el rol son obligatorios.",
        UserNotFoundException =>
            "No encontramos el usuario solicitado.",
        DuplicateEmailException =>
            "Ya existe un usuario con ese correo electrónico.",
        DuplicateIdNumberException =>
            "Ya existe un empleado con ese número de documento.",
        InvalidSupplierException =>
            "Revisa los datos del proveedor. El NIT, la razón social y la dirección son obligatorios, y la calificación debe estar entre 0 y 100.",
        SupplierNotFoundException =>
            "No encontramos el proveedor solicitado.",
        DuplicateNitException =>
            "Ya existe un proveedor con ese NIT.",
        InvalidInvoiceException =>
            "Revisa los datos de la factura. El cliente, el documento y al menos un ítem con cantidad y precio mayores que cero son obligatorios.",
        InvoiceNotFoundException =>
            "No encontramos la factura solicitada.",
        InvalidCustomerException =>
            "Revisa los datos del cliente. El documento y el nombre completo son obligatorios.",
        DuplicateCustomerDocumentException =>
            "Ya existe un cliente con ese documento.",
        CustomerNotFoundException =>
            "No encontramos el cliente solicitado.",
        InvalidPurchaseOrderException =>
            "La orden de compra no se puede completar con esos datos. Revisa las líneas, las cantidades y el estado de la orden.",
        PurchaseOrderNotFoundException =>
            "No encontramos la orden de compra solicitada.",
        InvalidProductSupplierException =>
            "Revisa la relación producto · proveedor. El producto, el proveedor activo y un último costo mayor que cero son obligatorios.",
        InvalidEmployeeException =>
            "Revisa los datos del empleado. El documento, el nombre, el cargo, el tipo de contrato y la tienda deben ser válidos, y el salario no puede ser negativo.",
        EmployeeNotFoundException =>
            "No encontramos el empleado solicitado.",
        InvalidStoreException =>
            "Revisa los datos de la tienda. El nombre y la dirección son obligatorios.",
        StoreNotFoundException =>
            "No encontramos la tienda solicitada.",
        DuplicateStoreNameException =>
            "Ya existe una tienda con ese nombre.",
        StoreHasStockException =>
            "No se puede desactivar la tienda porque todavía tiene unidades en stock.",
        StoreHasOpenOrdersException =>
            "No se puede desactivar la tienda porque tiene órdenes de compra abiertas.",
        StoreHasActiveUsersException =>
            "No se puede desactivar la tienda porque tiene usuarios activos asignados.",
        InvalidParameterException invalidParameter => invalidParameter.Message,
        ParameterNotFoundException =>
            "No encontramos el parámetro solicitado.",
        InvalidInventoryCountException =>
            "Revisa el conteo físico. Necesita al menos una línea, todas las cantidades contadas y un estado que permita la acción.",
        InventoryCountNotFoundException =>
            "No encontramos el conteo físico solicitado.",
        InvalidInventoryTransferException =>
            "Revisa el traslado. El origen y el destino deben ser tiendas activas distintas, cada línea necesita cantidad mayor que cero y el estado debe permitir la acción.",
        InventoryTransferNotFoundException =>
            "No encontramos el traslado solicitado.",
        InvalidPayrollInputException =>
            "Revisa los datos de la nómina. Los días trabajados deben estar entre 1 y 30 y la clase de riesgo entre 1 y 5.",
        UnsupportedContractTypeException =>
            "El tipo de contrato del empleado no tiene una regla de nómina configurada.",
        _ => UnexpectedMessage
    };
}
