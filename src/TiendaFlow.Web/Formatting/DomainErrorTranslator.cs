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
        // UserService already writes its rule violations in Spanish ("No puedes
        // desactivar tu propia cuenta", the password length, the last
        // administrator); only the English domain validations get the generic
        // message.
        InvalidUserException invalidUser when !ThrownByDomain(invalidUser) =>
            invalidUser.Message,
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
        InvalidSalesReturnException invalidReturn => ReturnMessage(invalidReturn),
        InvalidHrRecordException invalidHr => HrMessage(invalidHr),
        HrRecordNotFoundException =>
            "No encontramos el registro de talento humano solicitado.",
        InvalidCrmRecordException invalidCrm => CrmMessage(invalidCrm),
        CrmRecordNotFoundException =>
            "No encontramos el registro de CRM solicitado.",
        InvalidPosSaleException =>
            "No se pudo registrar la venta. Revisa el producto, la cantidad y que tu usuario tenga una tienda asignada.",
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
        Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException =>
            "Otro usuario modificó estos datos al mismo tiempo. Revisa los valores actuales e intenta de nuevo.",
        _ => UnexpectedMessage
    };

    // The closing modules raise English messages for code and tests; the user
    // sees the Spanish equivalent of each known rule.
    private static readonly Dictionary<string, string> RuleMessages = new(StringComparer.Ordinal)
    {
        ["Only an issued invoice accepts returns."] = "Solo una factura emitida admite devoluciones.",
        ["The invoice predates stock movements; cancel it instead."] = "La factura es anterior al control de inventario; anúlala en lugar de registrar una devolución.",
        ["Select at least one unit to return."] = "Indica al menos una unidad a devolver.",
        ["The reason is required."] = "El motivo es obligatorio.",
        ["The employee already has an active contract; end it first."] = "El empleado ya tiene un contrato activo; termínalo antes de registrar otro.",
        ["A fixed-term contract needs an end date."] = "Un contrato a término fijo necesita fecha de terminación.",
        ["The end date cannot be before the start date."] = "La fecha de terminación no puede ser anterior a la de inicio.",
        ["Only an active contract can be extended."] = "Solo se puede prorrogar un contrato activo.",
        ["An indefinite contract has no end date to extend."] = "Un contrato sin fecha de terminación no se prorroga.",
        ["The new end date must be after the current one."] = "La nueva fecha debe ser posterior a la actual.",
        ["The contract has already ended."] = "El contrato ya terminó.",
        ["A worked day needs check-in and check-out times."] = "Un día trabajado necesita hora de entrada y de salida.",
        ["The check-out must be after the check-in."] = "La salida debe ser posterior a la entrada.",
        ["The employee already has a record for that day."] = "El empleado ya tiene un registro para ese día.",
        ["The shift must end after it starts."] = "El turno debe terminar después de empezar.",
        ["The shift overlaps another shift of the same employee."] = "El turno se cruza con otro turno del mismo empleado.",
        ["The points to redeem must be greater than zero."] = "Los puntos a canjear deben ser mayores que cero.",
        ["A customer or a contact is required."] = "Indica el cliente o un dato de contacto.",
        ["The subject is required."] = "El asunto es obligatorio.",
        ["The description is required."] = "La descripción es obligatoria.",
        ["A closed PQRS cannot be reassigned."] = "Una PQRS cerrada no se puede reasignar.",
        ["The responsible person is required."] = "El responsable es obligatorio.",
        ["The PQRS is already closed."] = "La PQRS ya está cerrada.",
        ["The response is required to close the PQRS."] = "La respuesta es obligatoria para cerrar la PQRS.",
        ["The campaign name is required."] = "El nombre de la campaña es obligatorio.",
        ["The segment must be A, B or C."] = "El segmento debe ser A, B o C.",
        ["The campaign cannot end before it starts."] = "La campaña no puede terminar antes de empezar.",
        ["The campaign message is required."] = "El mensaje de la campaña es obligatorio."
    };

    private static string ReturnMessage(Exception exception)
    {
        if (RuleMessages.TryGetValue(exception.Message, out var text))
            return text;
        var match = System.Text.RegularExpressions.Regex.Match(
            exception.Message, @"^(.+): only (\d+) units can still be returned\.$");
        return match.Success
            ? $"{match.Groups[1].Value}: solo se pueden devolver {match.Groups[2].Value} unidades más."
            : "Revisa la devolución: cada línea necesita una cantidad válida de un producto de la factura.";
    }

    private static string HrMessage(Exception exception) =>
        RuleMessages.TryGetValue(exception.Message, out var text)
            ? text
            : "Revisa los datos del registro de talento humano.";

    private static string CrmMessage(Exception exception)
    {
        if (RuleMessages.TryGetValue(exception.Message, out var text))
            return text;
        var match = System.Text.RegularExpressions.Regex.Match(
            exception.Message, @"^The customer only has (-?\d+) points\.$");
        return match.Success
            ? $"El cliente solo tiene {match.Groups[1].Value} puntos."
            : "Revisa los datos del registro de CRM.";
    }

    private static bool ThrownByDomain(Exception exception) =>
        exception.TargetSite?.DeclaringType?.Assembly == typeof(AppUser).Assembly;
}
