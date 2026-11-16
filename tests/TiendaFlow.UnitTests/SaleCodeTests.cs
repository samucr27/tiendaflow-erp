using TiendaFlow.Application;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SaleCodeTests
{
    [Fact]
    public void Create_UsesTheUtcTimestampInTheReceiptCode()
    {
        var utc = new DateTime(2026, 11, 7, 15, 30, 45, DateTimeKind.Utc);

        var code = SaleCode.Create(utc);

        Assert.Equal("VTA-20261107153045", code);
    }

    [Fact]
    public void Create_NormalizesLocalTimestampsBeforeFormatting()
    {
        // Build the local value from a known UTC instant so the test does not
        // depend on the time zone of the machine that runs it.
        var utc = new DateTime(2026, 11, 7, 15, 30, 45, DateTimeKind.Utc);
        var local = utc.ToLocalTime();

        var code = SaleCode.Create(local);

        Assert.Equal("VTA-20261107153045", code);
    }
}