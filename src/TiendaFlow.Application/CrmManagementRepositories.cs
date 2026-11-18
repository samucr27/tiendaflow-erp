using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface ILoyaltyRepository
{
    LoyaltyMovement? GetById(int id);

    IReadOnlyList<LoyaltyMovement> List(
        DateOnly? from,
        DateOnly? to,
        int? storeId,
        int? customerId);

    int BalanceFor(int customerId);

    bool ReferenceExists(string reference, LoyaltyMovementType type);

    void Add(LoyaltyMovement movement);
}

public interface IPqrsRepository
{
    PqrsCase? GetById(int id);

    IReadOnlyList<PqrsCase> List(int? storeId, PqrsStatus? status);

    void Add(PqrsCase item);

    void Update(PqrsCase item);
}

public interface ICampaignRepository
{
    Campaign? GetById(int id);

    IReadOnlyList<Campaign> List();

    bool ExistsName(string name, int? exceptCampaignId = null);

    void Add(Campaign campaign);

    void Update(Campaign campaign);
}
