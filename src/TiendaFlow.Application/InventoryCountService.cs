using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Physical inventory counts (Fase 4). A count opens with a snapshot of the
/// system quantity, is closed only after every line has a counted quantity and
/// adjusts the stock once. The stock update, the document transition and the
/// audit share one transaction, and an adjusted count can no longer change.
/// </summary>
public sealed class InventoryCountService
{
    private readonly IInventoryCountRepository _counts;
    private readonly IStockRepository _stock;
    private readonly IStockQuery _stockQuery;
    private readonly IStoreRepository _stores;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public InventoryCountService(
        IInventoryCountRepository counts,
        IStockRepository stock,
        IStockQuery stockQuery,
        IStoreRepository stores,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _counts = counts;
        _stock = stock;
        _stockQuery = stockQuery;
        _stores = stores;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<InventoryStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.InventoryStockCount);

        return _stores.List()
            .Where(store => store.Active)
            .OrderBy(store => store.Name)
            .Select(store => new InventoryStoreOption(store.Id, store.Name))
            .ToList();
    }

    public int Create(int storeId, string? location)
    {
        _accessControl.Demand(Permissions.InventoryStockCount);

        var store = _stores.GetById(storeId) ?? throw new StoreNotFoundException(storeId);

        if (!store.Active)
            throw new InvalidInventoryCountException("An inactive store cannot be counted.");

        var normalizedLocation = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        var rows = _stockQuery.ListByStore(storeId)
            .Where(row => normalizedLocation is null
                || string.Equals(row.Location, normalizedLocation, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count == 0)
        {
            throw new InvalidInventoryCountException(normalizedLocation is null
                ? "The store has no stock rows to count."
                : $"The location {normalizedLocation} has no stock rows to count.");
        }

        var count = new InventoryCount(0, storeId, normalizedLocation, _clock.UtcNow);

        foreach (var row in rows
            .OrderBy(row => row.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Sku, StringComparer.Ordinal))
        {
            count.AddLine(row.Sku, row.QuantityAvailable);
        }

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _counts.Add(count);
            Audit(
                "InventoryCountOpened",
                count.Id.ToString(),
                $"Conteo {count.Id} abierto para la tienda {storeId}" +
                (normalizedLocation is null ? string.Empty : $" en {normalizedLocation}") +
                $": {count.Lines.Count} productos");
        });

        return count.Id;
    }

    public void SetCounted(int countId, string productSku, int countedQuantity)
    {
        _accessControl.Demand(Permissions.InventoryStockCount);

        var count = _counts.GetById(countId) ?? throw new InventoryCountNotFoundException(countId);
        count.SetCounted(productSku, countedQuantity);
        _counts.Update(count);
    }

    /// <summary>
    /// Saves the counted quantity of several lines at once so the screen can
    /// persist the whole sheet with a single update.
    /// </summary>
    public void SetCounted(int countId, IReadOnlyDictionary<string, int> quantities)
    {
        _accessControl.Demand(Permissions.InventoryStockCount);

        if (quantities.Count == 0)
            throw new InvalidInventoryCountException("No counted quantities were provided.");

        var count = _counts.GetById(countId) ?? throw new InventoryCountNotFoundException(countId);

        // Validate every pair before mutating the document so an invalid SKU
        // never leaves a half-updated sheet in the tracking context.
        var known = count.Lines
            .Select(line => line.ProductSku)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var pair in quantities)
        {
            if (pair.Value < 0)
                throw new InvalidInventoryCountException("The counted quantity cannot be negative.");

            var sku = pair.Key.Trim();

            if (!known.Contains(sku))
                throw new InvalidInventoryCountException($"{pair.Key} is not part of the count.");
        }

        foreach (var pair in quantities)
            count.SetCounted(pair.Key, pair.Value);

        _counts.Update(count);
    }

    public void Close(int countId)
    {
        _accessControl.Demand(Permissions.InventoryStockCount);

        var count = _counts.GetById(countId) ?? throw new InventoryCountNotFoundException(countId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            count.Close(_clock.UtcNow);
            _counts.Update(count);
            Audit(
                "InventoryCountClosed",
                count.Id.ToString(),
                $"Conteo {count.Id} cerrado: {count.Lines.Count} líneas, " +
                $"{count.Lines.Count(line => line.Difference != 0)} con diferencia");
        });
    }

    public void Adjust(int countId)
    {
        _accessControl.Demand(Permissions.InventoryStockCount);

        var count = _counts.GetById(countId) ?? throw new InventoryCountNotFoundException(countId);

        if (count.Status != InventoryCountStatus.Closed)
            throw new InvalidInventoryCountException("Only a closed count can be adjusted.");

        // Validate every stock row before mutating anything so a missing row
        // never leaves a half-applied adjustment in memory.
        var stockRows = new List<(InventoryCountLine Line, StockItem Stock)>();
        foreach (var line in count.Lines)
        {
            var stock = _stock.Get(count.StoreId, line.ProductSku)
                ?? throw new StockNotFoundException(count.StoreId, line.ProductSku);
            stockRows.Add((line, stock));
        }

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var now = _clock.UtcNow;

            foreach (var (line, stock) in stockRows)
            {
                stock.AdjustTo(line.CountedQuantity ?? line.SystemQuantity, now);
                _stock.Update(stock);
            }

            count.Adjust(now);
            _counts.Update(count);
            Audit(
                "InventoryCountAdjusted",
                count.Id.ToString(),
                DescribeAdjustment(count));
        });
    }

    private static string DescribeAdjustment(InventoryCount count)
    {
        var differences = count.Lines
            .Where(line => line.Difference != 0)
            .Select(line => $"{line.ProductSku} {(line.Difference > 0 ? "+" : string.Empty)}{line.Difference}")
            .ToList();

        if (differences.Count == 0)
            return $"Conteo {count.Id} ajustado sin diferencias";

        var net = count.Lines.Sum(line => line.Difference ?? 0);

        return $"Conteo {count.Id} ajustado: {string.Join(", ", differences)}; " +
               $"diferencia neta {net:+0;-0;0}";
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
