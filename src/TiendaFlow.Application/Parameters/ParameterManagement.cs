using System.Globalization;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Parameters;

public sealed record ParameterRow(
    string Key,
    string Name,
    string Description,
    ParameterValueType ValueType,
    string Value,
    string Unit,
    string SourceLabel,
    DateOnly? EffectiveFrom,
    int VersionCount,
    bool AllowsStoreScope,
    string DefaultValue);

public sealed record ParameterVersionRow(
    int Id,
    string Value,
    string ScopeLabel,
    string StoreName,
    DateOnly EffectiveFrom,
    int Version,
    string Author,
    DateTime CreatedAtUtc);

public sealed record ParameterHistory(
    string Key,
    string Name,
    string Description,
    ParameterValueType ValueType,
    string DefaultValue,
    string Unit,
    bool AllowsStoreScope,
    IReadOnlyList<ParameterVersionRow> Versions);

public sealed record ParameterStoreOption(int Id, string Name, bool Active);

public sealed record CreateParameterVersionRequest(
    string Key,
    ParameterScope Scope,
    int? StoreId,
    string Value,
    DateOnly EffectiveFrom);

public interface ISystemParameterRepository
{
    IReadOnlyList<SystemParameter> List();

    IReadOnlyList<SystemParameter> ListForKey(string key);

    SystemParameter? GetById(int id);

    SystemParameter? GetLatest(string key, ParameterScope scope, int? storeId, DateOnly asOf);

    void Add(SystemParameter parameter);
}

/// <summary>
/// Resolves the display name of a parameter author. Kept as a read model so the
/// parameter service never depends on the user administration repository.
/// </summary>
public interface IParameterAuthorQuery
{
    IReadOnlyDictionary<int, string> Names();
}

/// <summary>
/// Typed access to the current effective value of a parameter. The provider is
/// used internally by other modules (points, PQRS, replenishment, contracts and
/// shifts); it applies store override, then global value, then catalog default.
/// </summary>
public interface IParameterValues
{
    int GetInt(string key, int? storeId = null);

    decimal GetDecimal(string key, int? storeId = null);

    string GetText(string key, int? storeId = null);

    bool GetBoolean(string key, int? storeId = null);
}

/// <summary>
/// Parameter administration. Every version is append-only, audited and dated,
/// so an old value is never overwritten. Reads the latest version whose
/// effective date is on or before the requested business date.
/// </summary>
public sealed class ParameterService
{
    private readonly ISystemParameterRepository _parameters;
    private readonly IStoreRepository _stores;
    private readonly IParameterAuthorQuery _authors;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ParameterService(
        ISystemParameterRepository parameters,
        IStoreRepository stores,
        IParameterAuthorQuery authors,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _parameters = parameters;
        _stores = stores;
        _authors = authors;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<ParameterRow> List(int? storeId, DateOnly? asOf = null)
    {
        _accessControl.Demand(Permissions.SecurityParametersManage);

        if (storeId is int selectedStoreId && _stores.GetById(selectedStoreId) is null)
            throw new StoreNotFoundException(selectedStoreId);

        var date = asOf ?? Today();
        var rows = _parameters.List();

        return SystemParameterCatalog.All.Select(definition =>
        {
            var global = LatestFor(rows, definition.Key, ParameterScope.Global, null, date);
            var store = storeId is int id && definition.AllowsStoreScope
                ? LatestFor(rows, definition.Key, ParameterScope.Store, id, date)
                : null;
            var effective = store ?? global;

            return new ParameterRow(
                definition.Key,
                definition.Name,
                definition.Description,
                definition.ValueType,
                effective?.Value ?? definition.DefaultValue,
                definition.Unit,
                store is not null ? "Tienda" : global is not null ? "Global" : "Valor de código",
                effective?.EffectiveFrom,
                effective is null
                    ? 0
                    : rows.Count(row =>
                        row.Key == definition.Key
                        && row.Scope == effective.Scope
                        && row.StoreId == effective.StoreId),
                definition.AllowsStoreScope,
                definition.DefaultValue);
        }).ToList();
    }

    public ParameterHistory History(string key, ParameterScope scope, int? storeId)
    {
        _accessControl.Demand(Permissions.SecurityParametersManage);

        var definition = RequireDefinition(key);
        ValidateScope(definition, scope, storeId);

        if (storeId is int storeKey && _stores.GetById(storeKey) is null)
            throw new StoreNotFoundException(storeKey);

        var names = _authors.Names();

        var versions = _parameters
            .ListForKey(definition.Key)
            .Where(parameter => parameter.Scope == scope && parameter.StoreId == storeId)
            .OrderByDescending(parameter => parameter.EffectiveFrom)
            .ThenByDescending(parameter => parameter.Version)
            .Select(parameter => new ParameterVersionRow(
                parameter.Id,
                parameter.Value,
                ScopeLabel(parameter.Scope),
                parameter.StoreId is int id
                    ? _stores.GetById(id)?.Name ?? $"Tienda {id}"
                    : "Todas las tiendas",
                parameter.EffectiveFrom,
                parameter.Version,
                parameter.CreatedByUserId is int userId
                    && names.TryGetValue(userId, out var name)
                        ? name
                        : "Sistema",
                parameter.CreatedAt))
            .ToList();

        return new ParameterHistory(
            definition.Key,
            definition.Name,
            definition.Description,
            definition.ValueType,
            definition.DefaultValue,
            definition.Unit,
            definition.AllowsStoreScope,
            versions);
    }

    public IReadOnlyList<ParameterStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.SecurityParametersManage);

        return _stores.List()
            .OrderBy(store => store.Name)
            .Select(store => new ParameterStoreOption(store.Id, store.Name, store.Active))
            .ToList();
    }

    public int CreateVersion(CreateParameterVersionRequest request)
    {
        _accessControl.Demand(Permissions.SecurityParametersManage);

        var definition = RequireDefinition(request.Key);
        ValidateScope(definition, request.Scope, request.StoreId);

        var storeName = "Todas las tiendas";
        if (request.StoreId is int storeId)
        {
            var store = _stores.GetById(storeId) ?? throw new StoreNotFoundException(storeId);

            if (!store.Active)
                throw new InvalidParameterException(
                    "No se puede configurar un parámetro sobre una tienda inactiva.");

            storeName = store.Name;
        }

        var value = SystemParameter.NormalizeValue(definition.ValueType, request.Value);
        ValidateRange(definition, value);

        if (request.EffectiveFrom == default)
            throw new InvalidParameterException("La fecha de vigencia es obligatoria.");

        var existing = _parameters
            .ListForKey(definition.Key)
            .Where(parameter =>
                parameter.Scope == request.Scope
                && parameter.StoreId == request.StoreId)
            .ToList();

        if (existing.Any(parameter => parameter.EffectiveFrom == request.EffectiveFrom))
            throw new InvalidParameterException(
                "Ya existe una versión de este parámetro con esa fecha de vigencia.");

        var latest = existing
            .OrderByDescending(parameter => parameter.EffectiveFrom)
            .ThenByDescending(parameter => parameter.Version)
            .FirstOrDefault();

        if (latest is not null && request.EffectiveFrom < latest.EffectiveFrom)
            throw new InvalidParameterException(
                "La fecha de vigencia no puede ser anterior a la última versión registrada.");

        var version = existing.Count == 0 ? 1 : existing.Max(parameter => parameter.Version) + 1;
        var previousValue = latest?.Value ?? definition.DefaultValue;

        var parameter = new SystemParameter(
            definition.Key,
            definition.ValueType,
            request.Scope,
            request.StoreId,
            value,
            request.EffectiveFrom,
            version,
            _currentUser.UserId,
            _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _parameters.Add(parameter);
            Audit(
                "ParameterVersionCreated",
                definition.Key,
                $"Parámetro {definition.Key} ({ScopeLabel(request.Scope)}; {storeName}) "
                + $"v{version}: '{previousValue}' -> '{parameter.Value}', "
                + $"vigente desde {parameter.EffectiveFrom:yyyy-MM-dd}");
        });

        return parameter.Id;
    }

    private static ParameterDefinition RequireDefinition(string key) =>
        SystemParameterCatalog.Find(key)
        ?? throw new InvalidParameterException("El parámetro solicitado no existe en el catálogo.");

    private static void ValidateScope(ParameterDefinition definition, ParameterScope scope, int? storeId)
    {
        if (!Enum.IsDefined(scope))
            throw new InvalidParameterException("El alcance del parámetro no es válido.");

        if (scope == ParameterScope.Global && storeId is not null)
            throw new InvalidParameterException("Un parámetro global no puede tener tienda.");

        if (scope == ParameterScope.Store && storeId is not > 0)
            throw new InvalidParameterException("Selecciona la tienda del parámetro.");

        if (scope == ParameterScope.Store && !definition.AllowsStoreScope)
            throw new InvalidParameterException("Este parámetro no admite valores por tienda.");
    }

    private static void ValidateRange(ParameterDefinition definition, string value)
    {
        if (definition.Minimum is null && definition.Maximum is null)
            return;

        if (definition.ValueType is not (ParameterValueType.Integer or ParameterValueType.Decimal))
            return;

        var number = decimal.Parse(value, CultureInfo.InvariantCulture);

        if (definition.Minimum is decimal minimum && number < minimum)
            throw new InvalidParameterException(
                $"El valor debe ser mayor o igual a {minimum.ToString(CultureInfo.InvariantCulture)}.");

        if (definition.Maximum is decimal maximum && number > maximum)
            throw new InvalidParameterException(
                $"El valor debe ser menor o igual a {maximum.ToString(CultureInfo.InvariantCulture)}.");
    }

    private static SystemParameter? LatestFor(
        IReadOnlyList<SystemParameter> rows,
        string key,
        ParameterScope scope,
        int? storeId,
        DateOnly asOf) =>
        rows.Where(parameter =>
                parameter.Key == key
                && parameter.Scope == scope
                && parameter.StoreId == storeId
                && parameter.EffectiveFrom <= asOf)
            .OrderByDescending(parameter => parameter.EffectiveFrom)
            .ThenByDescending(parameter => parameter.Version)
            .FirstOrDefault();

    private static string ScopeLabel(ParameterScope scope) =>
        scope == ParameterScope.Global ? "Global" : "Tienda";

    private DateOnly Today() => DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private void Audit(string action, string entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId,
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "security",
            ip: _currentUser.Ip));
}

public sealed class ParameterValues : IParameterValues
{
    private readonly ISystemParameterRepository _parameters;
    private readonly IClock _clock;

    public ParameterValues(ISystemParameterRepository parameters, IClock clock)
    {
        _parameters = parameters;
        _clock = clock;
    }

    public int GetInt(string key, int? storeId = null) =>
        int.Parse(Resolve(key, storeId), CultureInfo.InvariantCulture);

    public decimal GetDecimal(string key, int? storeId = null) =>
        decimal.Parse(Resolve(key, storeId), CultureInfo.InvariantCulture);

    public string GetText(string key, int? storeId = null) => Resolve(key, storeId);

    public bool GetBoolean(string key, int? storeId = null) =>
        bool.Parse(Resolve(key, storeId));

    private string Resolve(string key, int? storeId)
    {
        var definition = SystemParameterCatalog.Find(key)
            ?? throw new ParameterNotFoundException(key);

        var today = DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

        if (storeId is int id && definition.AllowsStoreScope)
        {
            var storeValue = _parameters.GetLatest(key, ParameterScope.Store, id, today);
            if (storeValue is not null)
                return storeValue.Value;
        }

        var global = _parameters.GetLatest(key, ParameterScope.Global, null, today);
        return global?.Value ?? definition.DefaultValue;
    }
}
