namespace TiendaFlow.Domain;

public sealed record InventoryMovement
{
    public InventoryMovement(MovementType type, int quantity, DateTime date)
    {
        Type = type;
        Quantity = quantity;
        Date = UtcTimestamp.Normalize(date);
    }

    public MovementType Type { get; }

    public int Quantity { get; }

    public DateTime Date { get; }
}
