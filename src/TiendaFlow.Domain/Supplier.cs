namespace TiendaFlow.Domain;

/// <summary>
/// A company that supplies products to the stores. The rating is a percentage
/// (0 to 100) computed from the performance evaluations.
/// </summary>
public class Supplier
{
    public Supplier(
        int id,
        string nit,
        string businessName,
        string address,
        decimal rating = 100m,
        bool active = true)
    {
        RequireText(nit, "NIT");
        RequireText(businessName, "Business name");
        RequireText(address, "Address");
        RequireRating(rating);

        Id = id;
        Nit = nit.Trim();
        BusinessName = businessName.Trim();
        Address = address.Trim();
        Rating = rating;
        Active = active;
    }

    public int Id { get; private set; }

    public string Nit { get; }

    public string BusinessName { get; private set; }

    public string Address { get; private set; }

    public decimal Rating { get; private set; }

    public bool Active { get; private set; }

    public void UpdateDetails(string businessName, string address)
    {
        RequireText(businessName, "Business name");
        RequireText(address, "Address");

        BusinessName = businessName.Trim();
        Address = address.Trim();
    }

    public void ChangeRating(decimal rating)
    {
        RequireRating(rating);
        Rating = rating;
    }

    public void Deactivate() => Active = false;

    public void Activate() => Active = true;

    private static void RequireRating(decimal rating)
    {
        if (rating is < 0m or > 100m)
            throw new InvalidSupplierException("Rating must be between 0 and 100.");
    }

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidSupplierException($"{field} is required.");
    }
}
