namespace TiendaFlow.Domain;

/// <summary>
/// A physical store of the chain. Users and stock rows are scoped to a store.
/// </summary>
public class Store
{
    public Store(int id, string name, string address, bool active = true)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Store id must be positive.");
        RequireText(name, "Store name");
        RequireText(address, "Store address");

        Id = id;
        Name = name;
        Address = address;
        Active = active;
    }

    public int Id { get; }

    public string Name { get; private set; }

    public string Address { get; private set; }

    public bool Active { get; private set; }

    public void Rename(string name)
    {
        RequireText(name, "Store name");
        Name = name;
    }

    public void ChangeAddress(string address)
    {
        RequireText(address, "Store address");
        Address = address;
    }

    public void Deactivate() => Active = false;

    public void Activate() => Active = true;

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} is required.", nameof(value));
    }
}
