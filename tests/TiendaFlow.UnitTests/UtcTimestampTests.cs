using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class UtcTimestampTests
{
    [Fact]
    public void Normalize_WithUtcValue_KeepsTheSameInstant()
    {
        var value = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

        var normalized = UtcTimestamp.Normalize(value);

        Assert.Equal(value, normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void Normalize_WithLocalValue_ConvertsToUtc()
    {
        var value = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Local);

        var normalized = UtcTimestamp.Normalize(value);

        Assert.Equal(value.ToUniversalTime(), normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void Normalize_WithUnspecifiedValue_TreatsItAsUtc()
    {
        var value = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Unspecified);

        var normalized = UtcTimestamp.Normalize(value);

        Assert.Equal(value, normalized);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
    }

    [Fact]
    public void InventoryMovement_StoresItsDateInUtc()
    {
        var local = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Local);

        var movement = new InventoryMovement(MovementType.Receipt, 2, local);

        Assert.Equal(DateTimeKind.Utc, movement.Date.Kind);
        Assert.Equal(local.ToUniversalTime(), movement.Date);
    }

    [Fact]
    public void AuditEntry_StoresItsDateInUtc()
    {
        var local = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Local);

        var entry = new AuditEntry("PriceChanged", "SKU-001", "detail", 7, local);

        Assert.Equal(DateTimeKind.Utc, entry.Date.Kind);
        Assert.Equal(local.ToUniversalTime(), entry.Date);
    }
}
