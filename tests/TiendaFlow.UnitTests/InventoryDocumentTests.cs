using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InventoryCountTests
{
    private static readonly DateTime Date = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private static InventoryCount OpenWithLines()
    {
        var count = new InventoryCount(0, 1, "Bodega", Date);
        count.AddLine("SKU-2", 8);
        count.AddLine("SKU-1", 10);
        return count;
    }

    [Fact]
    public void New_StartsOpenAndNormalizesTheInput()
    {
        var count = new InventoryCount(0, 1, "  Bodega principal  ", Date);

        Assert.Equal(InventoryCountStatus.Open, count.Status);
        Assert.Equal("Bodega principal", count.Location);
        Assert.Equal(Date, count.CreatedAt);
        Assert.Null(count.ClosedAt);
        Assert.Null(count.AdjustedAt);
        Assert.Empty(count.Lines);
    }

    [Fact]
    public void New_WithoutLocation_KeepsItEmpty()
    {
        var count = new InventoryCount(0, 1, "   ", Date);

        Assert.Null(count.Location);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void New_WithoutAValidStore_Throws(int storeId)
    {
        Assert.Throws<InvalidInventoryCountException>(
            () => new InventoryCount(0, storeId, null, Date));
    }

    [Fact]
    public void AddLine_TrimsTheSkuAndStartsUncounted()
    {
        var count = new InventoryCount(0, 1, null, Date);

        count.AddLine("  SKU-1  ", 10);

        var line = Assert.Single(count.Lines);
        Assert.Equal("SKU-1", line.ProductSku);
        Assert.Equal(10, line.SystemQuantity);
        Assert.Null(line.CountedQuantity);
        Assert.Null(line.Difference);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddLine_WithoutSku_Throws(string sku)
    {
        var count = new InventoryCount(0, 1, null, Date);

        Assert.Throws<InvalidInventoryCountException>(() => count.AddLine(sku, 1));
    }

    [Fact]
    public void AddLine_WithNegativeSystemQuantity_Throws()
    {
        var count = new InventoryCount(0, 1, null, Date);

        Assert.Throws<InvalidInventoryCountException>(() => count.AddLine("SKU-1", -1));
    }

    [Fact]
    public void AddLine_WithARepeatedSku_Throws()
    {
        var count = OpenWithLines();

        Assert.Throws<InvalidInventoryCountException>(() => count.AddLine("SKU-1", 5));
    }

    [Fact]
    public void SetCounted_StoresTheQuantityAndTheDifference()
    {
        var count = OpenWithLines();

        count.SetCounted(" SKU-1 ", 7);

        var line = count.Lines.Single(item => item.ProductSku == "SKU-1");
        Assert.Equal(7, line.CountedQuantity);
        Assert.Equal(-3, line.Difference);
        Assert.True(count.HasDifferences);
    }

    [Fact]
    public void SetCounted_WithANegativeQuantity_Throws()
    {
        var count = OpenWithLines();

        Assert.Throws<InvalidInventoryCountException>(() => count.SetCounted("SKU-1", -1));
    }

    [Fact]
    public void SetCounted_WithAnUnknownSku_Throws()
    {
        var count = OpenWithLines();

        Assert.Throws<InvalidInventoryCountException>(() => count.SetCounted("SKU-404", 1));
    }

    [Fact]
    public void Close_RequiresAtLeastOneLine()
    {
        var count = new InventoryCount(0, 1, null, Date);

        Assert.Throws<InvalidInventoryCountException>(() => count.Close(Date));
    }

    [Fact]
    public void Close_RequiresEveryLineCounted()
    {
        var count = OpenWithLines();
        count.SetCounted("SKU-1", 10);

        Assert.Throws<InvalidInventoryCountException>(() => count.Close(Date));
        Assert.Equal(InventoryCountStatus.Open, count.Status);
    }

    [Fact]
    public void Close_SetsTheStatusAndTheTimestamp()
    {
        var count = OpenWithLines();
        count.SetCounted("SKU-1", 10);
        count.SetCounted("SKU-2", 8);

        count.Close(Date);

        Assert.Equal(InventoryCountStatus.Closed, count.Status);
        Assert.Equal(Date, count.ClosedAt);
        Assert.False(count.HasDifferences);
    }

    [Fact]
    public void Close_Twice_Throws()
    {
        var count = OpenWithLines();
        count.SetCounted("SKU-1", 10);
        count.SetCounted("SKU-2", 8);
        count.Close(Date);

        Assert.Throws<InvalidInventoryCountException>(() => count.Close(Date));
    }

    [Fact]
    public void Lines_AreImmutableAfterClosing()
    {
        var count = OpenWithLines();
        count.SetCounted("SKU-1", 10);
        count.SetCounted("SKU-2", 8);
        count.Close(Date);

        Assert.Throws<InvalidInventoryCountException>(() => count.AddLine("SKU-3", 1));
        Assert.Throws<InvalidInventoryCountException>(() => count.SetCounted("SKU-1", 1));
    }

    [Fact]
    public void Adjust_RequiresAClosedCount()
    {
        var count = OpenWithLines();

        Assert.Throws<InvalidInventoryCountException>(() => count.Adjust(Date));
    }

    [Fact]
    public void Adjust_SetsTheAdjustedStatusAndIsFinal()
    {
        var count = OpenWithLines();
        count.SetCounted("SKU-1", 10);
        count.SetCounted("SKU-2", 7);
        count.Close(Date);

        count.Adjust(Date);

        Assert.Equal(InventoryCountStatus.Adjusted, count.Status);
        Assert.Equal(Date, count.AdjustedAt);
        Assert.Throws<InvalidInventoryCountException>(() => count.Adjust(Date));
    }

    [Fact]
    public void Line_RejectsNegativeValues()
    {
        Assert.Throws<InvalidInventoryCountException>(() => new InventoryCountLine(1, "SKU-1", -1));

        var line = new InventoryCountLine(1, "SKU-1", 5);
        Assert.Throws<InvalidInventoryCountException>(() => line.SetCounted(-1));
    }
}

public class InventoryTransferTests
{
    private static readonly DateTime Date = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private static InventoryTransfer DraftWithLines()
    {
        var transfer = new InventoryTransfer(0, 1, 2, Date);
        transfer.AddLine("SKU-2", 4);
        transfer.AddLine("SKU-1", 6);
        return transfer;
    }

    [Fact]
    public void New_StartsAsDraft()
    {
        var transfer = new InventoryTransfer(0, 1, 2, Date);

        Assert.Equal(InventoryTransferStatus.Draft, transfer.Status);
        Assert.Equal(1, transfer.OriginStoreId);
        Assert.Equal(2, transfer.DestinationStoreId);
        Assert.Equal(0, transfer.TotalUnits);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    [InlineData(1, -2)]
    public void New_WithInvalidStores_Throws(int origin, int destination)
    {
        Assert.Throws<InvalidInventoryTransferException>(
            () => new InventoryTransfer(0, origin, destination, Date));
    }

    [Fact]
    public void New_WithTheSameStore_Throws()
    {
        Assert.Throws<InvalidInventoryTransferException>(
            () => new InventoryTransfer(0, 1, 1, Date));
    }

    [Fact]
    public void AddLine_TrimsTheSkuAndSumsTheUnits()
    {
        var transfer = new InventoryTransfer(0, 1, 2, Date);

        transfer.AddLine("  SKU-1  ", 5);
        transfer.AddLine("SKU-2", 3);

        Assert.Equal(8, transfer.TotalUnits);
        Assert.Equal("SKU-1", transfer.Lines[0].ProductSku);
        Assert.Equal("SKU-2", transfer.Lines[1].ProductSku);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("  ", 1)]
    [InlineData("SKU-1", 0)]
    [InlineData("SKU-1", -2)]
    public void AddLine_WithInvalidData_Throws(string sku, int quantity)
    {
        var transfer = new InventoryTransfer(0, 1, 2, Date);

        Assert.Throws<InvalidInventoryTransferException>(() => transfer.AddLine(sku, quantity));
    }

    [Fact]
    public void AddLine_WithARepeatedSku_Throws()
    {
        var transfer = DraftWithLines();

        Assert.Throws<InvalidInventoryTransferException>(() => transfer.AddLine("SKU-1", 1));
    }

    [Fact]
    public void Send_RequiresALine()
    {
        var transfer = new InventoryTransfer(0, 1, 2, Date);

        Assert.Throws<InvalidInventoryTransferException>(() => transfer.Send(Date));
    }

    [Fact]
    public void Send_SetsTheStatusAndTimestamp()
    {
        var transfer = DraftWithLines();

        transfer.Send(Date);

        Assert.Equal(InventoryTransferStatus.Sent, transfer.Status);
        Assert.Equal(Date, transfer.SentAt);
        Assert.Throws<InvalidInventoryTransferException>(() => transfer.AddLine("SKU-3", 1));
    }

    [Fact]
    public void Receive_RequiresASentTransfer()
    {
        var transfer = DraftWithLines();

        Assert.Throws<InvalidInventoryTransferException>(() => transfer.Receive(Date));
    }

    [Fact]
    public void Receive_SetsTheStatusAndTimestamp()
    {
        var transfer = DraftWithLines();
        transfer.Send(Date);

        transfer.Receive(Date.AddHours(1));

        Assert.Equal(InventoryTransferStatus.Received, transfer.Status);
        Assert.Equal(Date.AddHours(1), transfer.ReceivedAt);
        Assert.Throws<InvalidInventoryTransferException>(() => transfer.Receive(Date));
    }

    [Fact]
    public void Cancel_FromDraft_IsAllowed()
    {
        var transfer = DraftWithLines();

        transfer.Cancel(Date);

        Assert.Equal(InventoryTransferStatus.Cancelled, transfer.Status);
        Assert.Equal(Date, transfer.CancelledAt);
    }

    [Fact]
    public void Cancel_FromSent_IsAllowed()
    {
        var transfer = DraftWithLines();
        transfer.Send(Date);

        transfer.Cancel(Date.AddHours(1));

        Assert.Equal(InventoryTransferStatus.Cancelled, transfer.Status);
    }

    [Fact]
    public void Cancel_WhenReceived_Throws()
    {
        var transfer = DraftWithLines();
        transfer.Send(Date);
        transfer.Receive(Date);

        Assert.Throws<InvalidInventoryTransferException>(() => transfer.Cancel(Date));
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var transfer = DraftWithLines();
        transfer.Cancel(Date);

        Assert.Throws<InvalidInventoryTransferException>(() => transfer.Cancel(Date));
    }
}

public class StockItemTransferTests
{
    private static readonly DateTime Date = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RegisterTransferOut_DeductsAndRecordsTheMovement()
    {
        var stock = new StockItem("SKU-1", 1, 10, "Bodega");

        stock.RegisterTransferOut(4, Date);

        Assert.Equal(6, stock.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.TransferOut, 4, Date), stock.LastMovement);
    }

    [Fact]
    public void RegisterTransferOut_WithMoreUnitsThanAvailable_ThrowsAndKeepsTheStock()
    {
        var stock = new StockItem("SKU-1", 1, 3, "Bodega");

        Assert.Throws<InsufficientStockException>(() => stock.RegisterTransferOut(4, Date));
        Assert.Equal(3, stock.QuantityAvailable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterTransferOut_WithANonPositiveQuantity_Throws(int quantity)
    {
        var stock = new StockItem("SKU-1", 1, 10, "Bodega");

        Assert.Throws<ArgumentOutOfRangeException>(() => stock.RegisterTransferOut(quantity, Date));
    }

    [Fact]
    public void RegisterTransferIn_AddsAndRecordsTheMovement()
    {
        var stock = new StockItem("SKU-1", 1, 2, "Bodega");

        stock.RegisterTransferIn(5, Date);

        Assert.Equal(7, stock.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.TransferIn, 5, Date), stock.LastMovement);
    }

    [Fact]
    public void AdjustTo_SetsThePhysicalQuantityAndRecordsTheAbsoluteDifference()
    {
        var stock = new StockItem("SKU-1", 1, 10, "Bodega");

        stock.AdjustTo(7, Date);

        Assert.Equal(7, stock.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Adjustment, 3, Date), stock.LastMovement);
    }

    [Fact]
    public void AdjustTo_TheSameQuantity_DoesNotCreateAMovement()
    {
        var stock = new StockItem("SKU-1", 1, 10, "Bodega");
        stock.RegisterSale(1, Date);
        var movement = stock.LastMovement;

        stock.AdjustTo(9, Date.AddHours(1));

        Assert.Equal(9, stock.QuantityAvailable);
        Assert.Equal(movement, stock.LastMovement);
    }

    [Fact]
    public void AdjustTo_WithANegativeQuantity_Throws()
    {
        var stock = new StockItem("SKU-1", 1, 10, "Bodega");

        Assert.Throws<ArgumentOutOfRangeException>(() => stock.AdjustTo(-1, Date));
    }
}
