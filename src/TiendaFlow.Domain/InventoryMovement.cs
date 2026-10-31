namespace TiendaFlow.Domain;

public record InventoryMovement(MovementType Type, int Quantity, DateTime Date);
