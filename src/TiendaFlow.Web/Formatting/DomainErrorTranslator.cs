using TiendaFlow.Application.Payroll;
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
        InvalidPurchaseOrderException =>
            "La orden de compra no se puede completar con esos datos. Revisa las líneas, las cantidades y el estado de la orden.",
        PurchaseOrderNotFoundException =>
            "No encontramos la orden de compra solicitada.",
        InvalidEmployeeException =>
            "Revisa los datos del empleado. El documento, el nombre, el cargo, el tipo de contrato y la tienda deben ser válidos, y el salario no puede ser negativo.",
        EmployeeNotFoundException =>
            "No encontramos el empleado solicitado.",
        InvalidPayrollInputException =>
            "Revisa los datos de la nómina. Los días trabajados deben estar entre 1 y 30 y la clase de riesgo entre 1 y 5.",
        UnsupportedContractTypeException =>
            "El tipo de contrato del empleado no tiene una regla de nómina configurada.",
        _ => "Ocurrió un error inesperado. Intenta de nuevo."
    };
}
