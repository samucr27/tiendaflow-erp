using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Transfers between stores (Fase 4). Sending validates and deducts every line
/// from the origin, receiving adds the destination (creating the stock row when
/// the product was never sold there) and cancelling a sent transfer returns the
/// units to the origin. Each transition, its stock movements and its audit run
/// in a single transaction.
/// </summary>
public sealed class InventoryTransferService
{
    private readonly IInventoryTransferRepository _transfers;
    private readonly IStockRepository _stock;
    private readonly IStoreRepository _stores;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public InventoryTransferService(
        IInventoryTransferRepository transfers,
        IStockRepository stock,
        IStoreRepository stores,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _transfers = transfers;
        _stock = stock;
        _stores = stores;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<InventoryStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.InventoryStockTransfer);

        return _stores.List()
            .Where(store => store.Active)
            .OrderBy(store => store.Name)
            .Select(store => new InventoryStoreOption(store.Id, store.Name))
            .ToList();
    }

    public int Create(CreateInventoryTransferRequest request)
    {
        _accessControl.Demand(Permissions.InventoryStockTransfer);

        var origin = _stores.GetById(request.OriginStoreId)
            ?? throw new StoreNotFoundException(request.OriginStoreId);
        var destination = _stores.GetById(request.DestinationStoreId)
            ?? throw new StoreNotFoundException(request.DestinationStoreId);

        if (!origin.Active || !destination.Active)
            throw new InvalidInventoryTransferException("Both stores must be active.");

        if (request.Lines.Count == 0)
            throw new InvalidInventoryTransferException("A transfer needs at least one line.");

        var transfer = new InventoryTransfer(
            0,
            request.OriginStoreId,
            request.DestinationStoreId,
            _clock.UtcNow);

        foreach (var line in request.Lines)
            transfer.AddLine(line.ProductSku, line.Quantity);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _transfers.Add(transfer);
            Audit(
                "InventoryTransferCreated",
                transfer.Id.ToString(),
                $"Traslado {transfer.Id} creado de tienda {transfer.OriginStoreId} " +
                $"a tienda {transfer.DestinationStoreId}: {transfer.TotalUnits} unidades");
        });

        return transfer.Id;
    }

    public void Send(int transferId)
    {
        _accessControl.Demand(Permissions.InventoryStockTransfer);

        var transfer = _transfers.GetById(transferId)
            ?? throw new InventoryTransferNotFoundException(transferId);

        if (transfer.Status != InventoryTransferStatus.Draft)
            throw new InvalidInventoryTransferException(
                $"Cannot send a transfer in status {transfer.Status}.");

        // Validate every line before mutating the document. A missing row or a
        // short quantity aborts the whole send.
        var stockRows = transfer.Lines.Select(line =>
        {
            var stock = _stock.Get(transfer.OriginStoreId, line.ProductSku)
                ?? throw new InsufficientStockException(
                    line.ProductSku,
                    available: 0,
                    requested: line.Quantity);

            if (stock.QuantityAvailable < line.Quantity)
                throw new InsufficientStockException(
                    line.ProductSku,
                    stock.QuantityAvailable,
                    line.Quantity);

            return (Line: line, Stock: stock);
        }).ToList();

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var now = _clock.UtcNow;
            transfer.Send(now);

            foreach (var (line, stock) in stockRows)
            {
                stock.RegisterTransferOut(line.Quantity, now);
                _stock.Update(stock);
            }

            _transfers.Update(transfer);
            Audit(
                "InventoryTransferSent",
                transfer.Id.ToString(),
                $"Traslado {transfer.Id} enviado: {transfer.TotalUnits} unidades " +
                $"de tienda {transfer.OriginStoreId} a tienda {transfer.DestinationStoreId}");
        });
    }

    public void Receive(int transferId)
    {
        _accessControl.Demand(Permissions.InventoryStockTransfer);

        var transfer = _transfers.GetById(transferId)
            ?? throw new InventoryTransferNotFoundException(transferId);

        if (transfer.Status != InventoryTransferStatus.Sent)
            throw new InvalidInventoryTransferException(
                $"Cannot receive a transfer in status {transfer.Status}.");

        var stockRows = transfer.Lines.Select(line =>
        {
            var stock = _stock.Get(transfer.DestinationStoreId, line.ProductSku);
            var isNew = stock is null;
            stock ??= new StockItem(
                line.ProductSku,
                transfer.DestinationStoreId,
                0,
                "Recepción de traslado");
            return (Line: line, Stock: stock, IsNew: isNew);
        }).ToList();

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var now = _clock.UtcNow;
            transfer.Receive(now);

            foreach (var (line, stock, isNew) in stockRows)
            {
                stock.RegisterTransferIn(line.Quantity, now);

                if (isNew)
                    _stock.Add(stock);
                else
                    _stock.Update(stock);
            }

            _transfers.Update(transfer);
            Audit(
                "InventoryTransferReceived",
                transfer.Id.ToString(),
                $"Traslado {transfer.Id} recibido: {transfer.TotalUnits} unidades " +
                $"en tienda {transfer.DestinationStoreId}");
        });
    }

    public void Cancel(int transferId)
    {
        _accessControl.Demand(Permissions.InventoryStockTransfer);

        var transfer = _transfers.GetById(transferId)
            ?? throw new InventoryTransferNotFoundException(transferId);

        if (transfer.Status is not (InventoryTransferStatus.Draft or InventoryTransferStatus.Sent))
            throw new InvalidInventoryTransferException(
                $"Cannot cancel a transfer in status {transfer.Status}.");

        var returnsStock = transfer.Status == InventoryTransferStatus.Sent;
        var stockRows = returnsStock
            ? transfer.Lines.Select(line =>
            {
                var stock = _stock.Get(transfer.OriginStoreId, line.ProductSku);
                var isNew = stock is null;
                stock ??= new StockItem(
                    line.ProductSku,
                    transfer.OriginStoreId,
                    0,
                    "Devolución de traslado");
                return (Line: line, Stock: stock, IsNew: isNew);
            }).ToList()
            : new List<(InventoryTransferLine Line, StockItem Stock, bool IsNew)>();

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var now = _clock.UtcNow;
            transfer.Cancel(now);

            foreach (var (line, stock, isNew) in stockRows)
            {
                stock.RegisterTransferIn(line.Quantity, now);

                if (isNew)
                    _stock.Add(stock);
                else
                    _stock.Update(stock);
            }

            _transfers.Update(transfer);
            Audit(
                "InventoryTransferCancelled",
                transfer.Id.ToString(),
                $"Traslado {transfer.Id} anulado desde estado " +
                $"{(returnsStock ? InventoryTransferStatus.Sent : InventoryTransferStatus.Draft)}" +
                (returnsStock ? $"; {transfer.TotalUnits} unidades devueltas a tienda {transfer.OriginStoreId}" : string.Empty));
        });
    }

    private void Audit(string action, string entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId,
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "inventory",
            ip: _currentUser.Ip));
}
