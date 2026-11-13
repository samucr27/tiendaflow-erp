using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Domain;

namespace TiendaFlow.UnitTests;

internal sealed class InMemorySystemParameterRepository : ISystemParameterRepository
{
    private readonly List<SystemParameter> _parameters = new();
    private int _nextId = 1;

    public int AddCalls { get; private set; }

    public int ListCalls { get; private set; }

    public int ListForKeyCalls { get; private set; }

    public int GetLatestCalls { get; private set; }

    public IReadOnlyList<SystemParameter> List()
    {
        ListCalls++;
        return _parameters.ToList();
    }

    public IReadOnlyList<SystemParameter> ListForKey(string key)
    {
        ListForKeyCalls++;
        var normalized = key.Trim();
        return _parameters.Where(parameter => parameter.Key == normalized).ToList();
    }

    public SystemParameter? GetById(int id) =>
        _parameters.FirstOrDefault(parameter => parameter.Id == id);

    public SystemParameter? GetLatest(
        string key,
        ParameterScope scope,
        int? storeId,
        DateOnly asOf)
    {
        GetLatestCalls++;
        var normalized = key.Trim();

        return _parameters
            .Where(parameter =>
                parameter.Key == normalized
                && parameter.Scope == scope
                && parameter.StoreId == storeId
                && parameter.EffectiveFrom <= asOf)
            .OrderByDescending(parameter => parameter.EffectiveFrom)
            .ThenByDescending(parameter => parameter.Version)
            .FirstOrDefault();
    }

    public void Add(SystemParameter parameter)
    {
        AddCalls++;
        TestIds.Assign(parameter, _nextId++);
        _parameters.Add(parameter);
    }
}

internal sealed class FakeParameterAuthorQuery : IParameterAuthorQuery
{
    public Dictionary<int, string> Authors { get; } = new();

    public int Calls { get; private set; }

    public IReadOnlyDictionary<int, string> Names()
    {
        Calls++;
        return Authors;
    }
}

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = UtcNowValue(utcNow);

    public DateTime UtcNow { get; }

    private static DateTime UtcNowValue(DateTime value) => UtcTimestamp.Normalize(value);
}
