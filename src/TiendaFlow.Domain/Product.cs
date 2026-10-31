namespace TiendaFlow.Domain;

public class Product
{
    public Product(string sku, string barcode, string name, decimal salePrice)
    {
        Validate(sku, barcode, name, salePrice);

        Sku = sku;
        Barcode = barcode;
        Name = name;
        SalePrice = salePrice;
    }

    public string Sku { get; }
    public string Barcode { get; }
    public string Name { get; }
    public decimal SalePrice { get; private set; }

    public void ChangePrice(decimal newPrice) { }

    private static void Validate(string sku, string barcode, string name, decimal salePrice)
    {
        RequireText(sku, "SKU");
        RequireText(barcode, "Barcode");
        RequireText(name, "Name");

        if (salePrice <= 0)
            throw new InvalidProductException("Sale price must be greater than zero.");
    }

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidProductException($"{field} is required.");
    }
}
