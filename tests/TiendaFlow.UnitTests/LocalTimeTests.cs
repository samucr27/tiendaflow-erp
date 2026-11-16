using TiendaFlow.Application;
using Xunit;

namespace TiendaFlow.UnitTests;

public class LocalTimeTests
{
    [Fact]
    public void ToLocal_ConvertsUtcToColombiaTime()
    {
        var utc = new DateTime(2026, 11, 7, 15, 30, 0, DateTimeKind.Utc);

        var local = LocalTime.ToLocal(utc);

        Assert.Equal(new DateTime(2026, 11, 7, 10, 30, 0), local);
    }

    [Fact]
    public void ToLocal_UsesTheSameInstantForEveryDayOfTheYear()
    {
        var utc = new DateTime(2026, 1, 15, 5, 0, 0, DateTimeKind.Utc);

        var local = LocalTime.ToLocal(utc);

        Assert.Equal(new DateTime(2026, 1, 15, 0, 0, 0), local);
    }

    [Fact]
    public void ToLocal_WithLocalKind_DoesNotShiftTwice()
    {
        var utc = new DateTime(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

        var converted = LocalTime.ToLocal(utc.ToLocalTime());

        Assert.Equal(LocalTime.ToLocal(utc), converted);
    }
}
