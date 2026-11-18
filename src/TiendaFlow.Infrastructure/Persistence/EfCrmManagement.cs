using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfLoyaltyRepository : ILoyaltyRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfLoyaltyRepository(TiendaFlowDbContext context) => _context = context;

    public LoyaltyMovement? GetById(int id) =>
        _context.LoadFresh<LoyaltyMovement>(
            movement => movement.Id == id,
            () => _context.LoyaltyMovements.FirstOrDefault(movement => movement.Id == id));

    public IReadOnlyList<LoyaltyMovement> List(
        DateOnly? from,
        DateOnly? to,
        int? storeId,
        int? customerId)
    {
        var query = _context.LoyaltyMovements.AsNoTracking().AsQueryable();

        if (from is DateOnly start)
            query = query.Where(movement => movement.CreatedAt >= LocalTime.ToUtc(start));
        if (to is DateOnly end)
            query = query.Where(movement => movement.CreatedAt < LocalTime.ToUtc(end.AddDays(1)));
        if (storeId is int store)
            query = query.Where(movement => movement.StoreId == store);
        if (customerId is int customer)
            query = query.Where(movement => movement.CustomerId == customer);

        return query
            .OrderByDescending(movement => movement.CreatedAt)
            .ThenBy(movement => movement.Id)
            .ToList();
    }

    public int BalanceFor(int customerId) =>
        _context.LoyaltyMovements
            .AsNoTracking()
            .Where(movement => movement.CustomerId == customerId)
            .ToList()
            .Sum(movement => movement.SignedPoints);

    public bool ReferenceExists(string reference, LoyaltyMovementType type)
    {
        var normalized = reference.Trim();

        return _context.LoyaltyMovements
            .AsNoTracking()
            .Any(movement => movement.Reference == normalized && movement.Type == type);
    }

    public void Add(LoyaltyMovement movement)
    {
        _context.LoyaltyMovements.Add(movement);
        _context.SaveChanges();
    }
}

public sealed class EfPqrsRepository : IPqrsRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfPqrsRepository(TiendaFlowDbContext context) => _context = context;

    public PqrsCase? GetById(int id) =>
        _context.LoadFresh<PqrsCase>(
            item => item.Id == id,
            () => _context.PqrsCases.FirstOrDefault(item => item.Id == id));

    public IReadOnlyList<PqrsCase> List(int? storeId, PqrsStatus? status)
    {
        var query = _context.PqrsCases.AsNoTracking().AsQueryable();

        if (storeId is int store)
            query = query.Where(item => item.StoreId == store);
        if (status is PqrsStatus value)
            query = query.Where(item => item.Status == value);

        return query
            .OrderByDescending(item => item.CreatedDate)
            .ThenBy(item => item.Id)
            .ToList();
    }

    public void Add(PqrsCase item)
    {
        _context.PqrsCases.Add(item);
        _context.SaveChanges();
    }

    public void Update(PqrsCase item)
    {
        if (_context.Entry(item).State == EntityState.Detached)
            _context.PqrsCases.Update(item);

        _context.SaveChanges();
    }
}

public sealed class EfCampaignRepository : ICampaignRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfCampaignRepository(TiendaFlowDbContext context) => _context = context;

    public Campaign? GetById(int id) =>
        _context.LoadFresh<Campaign>(
            campaign => campaign.Id == id,
            () => _context.Campaigns.FirstOrDefault(campaign => campaign.Id == id));

    public IReadOnlyList<Campaign> List() =>
        _context.Campaigns
            .AsNoTracking()
            .OrderBy(campaign => campaign.StartDate)
            .ThenBy(campaign => campaign.Name)
            .ToList();

    public bool ExistsName(string name, int? exceptCampaignId = null)
    {
        var normalized = name.Trim();
        var exceptId = exceptCampaignId;

        return _context.Campaigns
            .AsNoTracking()
            .Any(campaign => campaign.Name == normalized
                && (!exceptId.HasValue || campaign.Id != exceptId.Value));
    }

    public void Add(Campaign campaign)
    {
        _context.Campaigns.Add(campaign);
        _context.SaveChanges();
    }

    public void Update(Campaign campaign)
    {
        if (_context.Entry(campaign).State == EntityState.Detached)
            _context.Campaigns.Update(campaign);

        _context.SaveChanges();
    }
}
