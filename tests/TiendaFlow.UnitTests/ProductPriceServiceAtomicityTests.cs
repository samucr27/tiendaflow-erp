using Moq;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductPriceServiceAtomicityTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    private readonly Mock<IProductRepository> _repository = new();
    private readonly Mock<IAuditLog> _auditLog = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();

    private ProductPriceService CreateService() =>
        new(_repository.Object, _auditLog.Object, _unitOfWork);

    private void GivenProduct()
    {
        var product = new Product("SKU-001", "7701234567890", "Soda 400 ml", 3500m);
        _repository.Setup(repository => repository.GetBySku("SKU-001")).Returns(product);
    }

    [Fact]
    public void UpdatePrice_RunsThePriceChangeAndTheAuditInsideTheUnitOfWork()
    {
        GivenProduct();

        CreateService().UpdatePrice("SKU-001", 3900m, 7, Now);

        Assert.True(_unitOfWork.WasExecuted);
        Assert.False(_unitOfWork.RolledBack);
        _auditLog.Verify(log => log.Record(It.IsAny<AuditEntry>()), Times.Once);
    }

    [Fact]
    public void UpdatePrice_WhenTheAuditFails_RollsBackTheUnitOfWorkAndRethrows()
    {
        GivenProduct();
        _auditLog
            .Setup(log => log.Record(It.IsAny<AuditEntry>()))
            .Throws(new InvalidOperationException("Simulated audit failure."));

        Assert.Throws<InvalidOperationException>(
            () => CreateService().UpdatePrice("SKU-001", 3900m, 7, Now));

        Assert.True(_unitOfWork.WasExecuted);
        Assert.True(_unitOfWork.RolledBack);
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public bool WasExecuted { get; private set; }

        public bool RolledBack { get; private set; }

        public void ExecuteInTransaction(Action action)
        {
            WasExecuted = true;

            try
            {
                action();
            }
            catch
            {
                RolledBack = true;
                throw;
            }
        }
    }
}
