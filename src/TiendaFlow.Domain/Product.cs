namespace TiendaFlow.Domain;

public class Product
{
    public Product(string sku, string barcode, string name, decimal salePrice)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidProductException("SKU is required.");
        if (string.IsNullOrWhiteSpace(barcode))
            throw new InvalidProductException("Barcode is required.");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Name is required.");
        if (salePrice <= 0)
            throw new InvalidProductException("Sale price must be greater than zero.");

        Sku = sku;
        Barcode = barcode;
        Name = name;
        SalePrice = salePrice;
    }

    public string Sku { get; }
    public string Barcode { get; }
    public string Name { get; }
    public decimal SalePrice { get; }
}
