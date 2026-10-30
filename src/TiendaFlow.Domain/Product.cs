namespace TiendaFlow.Domain;

public class Product
{
    public Product(string sku, string barcode, string name, decimal salePrice)
    {
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
