using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class PurchaseOrderTests
{
    private static readonly DateTime Date = new(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

    private static PurchaseOrder DraftWithLines()
    {
        var order = new PurchaseOrder(7, 5, 1, Date);
        order.AddLine("SKU-1", 10, 1000m);
        order.AddLine("SKU-2", 5, 500m);
        return order;
    }

    private static PurchaseOrder SentWithLines()
    {
        var order = DraftWithLines();
        order.Approve();
        order.Send();
        return order;
    }

    [Fact]
    public void New_StartsAsDraftWithZeroTotal()
    {
        var order = new PurchaseOrder(7, 5, 1, Date);

        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal(0m, order.Total);
        Assert.Empty(order.Lines);
        Assert.Equal(Date, order.OrderDate);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(5, 0)]
    public void New_WithoutSupplierOrStore_Throws(int supplierId, int storeId)
    {
        Assert.Throws<InvalidPurchaseOrderException>(
            () => new PurchaseOrder(1, supplierId, storeId, Date));
    }

    [Fact]
    public void AddLine_RecalculatesTheTotal()
    {
        var order = DraftWithLines();

        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(12500m, order.Total);
    }

    [Fact]
    public void AddLine_WithRepeatedSku_Throws()
    {
        var order = DraftWithLines();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.AddLine("SKU-1", 1, 100m));
    }

    [Fact]
    public void AddLine_AfterSubmitting_Throws()
    {
        var order = DraftWithLines();
        order.SubmitForApproval();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.AddLine("SKU-3", 1, 100m));
    }

    [Fact]
    public void Workflow_MovesThroughEveryStatus()
    {
        var order = DraftWithLines();

        order.SubmitForApproval();
        Assert.Equal(PurchaseOrderStatus.PendingApproval, order.Status);

        order.Approve();
        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);

        order.Send();
        Assert.Equal(PurchaseOrderStatus.Sent, order.Status);

        order.Confirm();
        Assert.Equal(PurchaseOrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Approve_FromDraft_IsAllowed()
    {
        var order = DraftWithLines();

        order.Approve();

        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
    }

    [Fact]
    public void Approve_WhenAlreadySent_Throws()
    {
        var order = SentWithLines();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Approve());
    }

    [Fact]
    public void Send_BeforeApproval_Throws()
    {
        var order = DraftWithLines();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Send());
    }

    [Fact]
    public void SubmitForApproval_Twice_Throws()
    {
        var order = DraftWithLines();
        order.SubmitForApproval();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.SubmitForApproval());
    }

    [Fact]
    public void Confirm_BeforeSending_Throws()
    {
        var order = DraftWithLines();
        order.Approve();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Confirm());
    }

    [Fact]
    public void ReceiveLine_PartiallyThenTotally_UpdatesTheStatus()
    {
        var order = SentWithLines();

        order.ReceiveLine("SKU-1", 4);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);

        order.ReceiveLine("SKU-1", 6);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);

        order.ReceiveLine("SKU-2", 5);
        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
    }

    [Fact]
    public void ReceiveLine_WhenConfirmed_IsAllowed()
    {
        var order = SentWithLines();
        order.Confirm();

        order.ReceiveLine("SKU-1", 10);

        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
    }

    [Fact]
    public void ReceiveLine_BeforeSending_Throws()
    {
        var order = DraftWithLines();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.ReceiveLine("SKU-1", 1));
    }

    [Fact]
    public void ReceiveLine_WithUnknownSku_Throws()
    {
        var order = SentWithLines();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.ReceiveLine("SKU-404", 1));
    }

    [Fact]
    public void ReceiveLine_MoreThanPending_Throws()
    {
        var order = SentWithLines();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.ReceiveLine("SKU-1", 11));
    }

    [Fact]
    public void Cancel_FromDraft_Cancels()
    {
        var order = DraftWithLines();

        order.Cancel();

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_Twice_Throws()
    {
        var order = DraftWithLines();
        order.Cancel();

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Cancel());
    }

    [Fact]
    public void Cancel_WithReceivedUnits_Throws()
    {
        var order = SentWithLines();
        order.ReceiveLine("SKU-1", 1);

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Cancel());
    }

    [Fact]
    public void Cancel_WhenFullyReceived_Throws()
    {
        var order = SentWithLines();
        order.ReceiveLine("SKU-1", 10);
        order.ReceiveLine("SKU-2", 5);

        Assert.Throws<InvalidPurchaseOrderException>(() => order.Cancel());
    }
}

public class PurchaseOrderLineTests
{
    [Fact]
    public void New_TrimsTheSkuAndComputesTotals()
    {
        var line = new PurchaseOrderLine(1, " SKU-1 ", 10, 1000m, receivedQuantity: 4);

        Assert.Equal("SKU-1", line.ProductSku);
        Assert.Equal(6, line.PendingQuantity);
        Assert.Equal(10000m, line.LineTotal);
    }

    [Theory]
    [InlineData("", 1, 100)]
    [InlineData("  ", 1, 100)]
    [InlineData("SKU-1", 0, 100)]
    [InlineData("SKU-1", -1, 100)]
    [InlineData("SKU-1", 1, 0)]
    [InlineData("SKU-1", 1, -5)]
    public void New_WithInvalidData_Throws(string sku, int quantity, int unitCost)
    {
        Assert.Throws<InvalidPurchaseOrderException>(
            () => new PurchaseOrderLine(1, sku, quantity, unitCost));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void New_WithReceivedQuantityOutOfRange_Throws(int received)
    {
        Assert.Throws<InvalidPurchaseOrderException>(
            () => new PurchaseOrderLine(1, "SKU-1", 10, 1000m, received));
    }

    [Fact]
    public void Receive_AddsToTheReceivedQuantity()
    {
        var line = new PurchaseOrderLine(1, "SKU-1", 10, 1000m);

        line.Receive(3);
        line.Receive(7);

        Assert.Equal(10, line.ReceivedQuantity);
        Assert.Equal(0, line.PendingQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(11)]
    public void Receive_WithInvalidQuantity_Throws(int quantity)
    {
        var line = new PurchaseOrderLine(1, "SKU-1", 10, 1000m);

        Assert.Throws<InvalidPurchaseOrderException>(() => line.Receive(quantity));
    }
}