namespace TiendaFlow.Domain;

/// <summary>
/// A customer registered by the store for service and loyalty. The document
/// is the business key; personal data is kept as a snapshot for the CRM.
/// Segmentation and campaigns are out of scope for this sprint.
/// </summary>
public class Customer
{
    public Customer(
        int id,
        string documentNumber,
        string fullName,
        string? email = null,
        string? phone = null,
        string? address = null,
        bool active = true)
    {
        RequireText(documentNumber, "The document number is required.");
        RequireText(fullName, "The full name is required.");

        Id = id;
        DocumentNumber = documentNumber.Trim();
        FullName = fullName.Trim();
        Email = Normalize(email);
        Phone = Normalize(phone);
        Address = Normalize(address);
        Active = active;
    }

    public int Id { get; private set; }

    public string DocumentNumber { get; }

    public string FullName { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Address { get; private set; }

    public bool Active { get; private set; }

    public void UpdateDetails(
        string fullName,
        string? email,
        string? phone,
        string? address)
    {
        RequireText(fullName, "The full name is required.");

        FullName = fullName.Trim();
        Email = Normalize(email);
        Phone = Normalize(phone);
        Address = Normalize(address);
    }

    public void Deactivate() => Active = false;

    public void Activate() => Active = true;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void RequireText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidCustomerException(message);
    }
}
