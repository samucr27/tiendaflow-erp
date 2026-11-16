namespace TiendaFlow.Domain;

/// <summary>
/// The relation between a product and a supplier. A product may be bought
/// from several suppliers, but only one of them is the default used by the
/// automatic replenishment drafts. The last cost is the price of the latest
/// purchase known for that pair.
/// </summary>
public class ProductSupplier
{
    public ProductSupplier(
        string productSku,
        int supplierId,
        decimal lastCost,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidProductSupplierException("The product SKU is required.");
        if (supplierId <= 0)
            throw new InvalidProductSupplierException("A supplier is required.");
        RequirementPositiveCost(lastCost);

        ProductSku = productSku.Trim();
        SupplierId = supplierId;
        LastCost = lastCost;
        IsDefault = isDefault;
    }

    public string ProductSku { get; private set; }

    public int SupplierId { get; private set; }

    public decimal LastCost { get; private set; }

    public bool IsDefault { get; private set; }

    public void ChangeCost(decimal lastCost)
    {
        RequirementPositiveCost(lastCost);
        LastCost = lastCost;
    }

    public void MarkAsDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

    private static void RequirementPositiveCost(decimal lastCost)
    {
        if (lastCost <= 0m)
            throw new InvalidProductSupplierException(
                "The last cost must be greater than zero.");
    }
}
