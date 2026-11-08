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
        var local = new DateTime(2026, 11, 7, 10, 30, 45, DateTimeKind.Local);

        var code = SaleCode.Create(local);

        Assert.Equal("VTA-20261107153045", code);
    }
}
