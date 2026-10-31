namespace TiendaFlow.Domain;

public class DuplicateBarcodeException : Exception
{
    public DuplicateBarcodeException(string barcode)
        : base($"Barcode {barcode} is already registered.") { }
}
