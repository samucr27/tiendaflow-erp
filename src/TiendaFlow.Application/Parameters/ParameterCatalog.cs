using TiendaFlow.Domain;

namespace TiendaFlow.Application.Parameters;

/// <summary>
/// The parameters the ERP understands. The catalog is code so an unknown key
/// can never be stored, and every key keeps a documented default for the case
/// where the database has no version yet.
/// </summary>
public sealed record ParameterDefinition(
    string Key,
    string Name,
    string Description,
    ParameterValueType ValueType,
    string DefaultValue,
    string Unit,
    decimal? Minimum = null,
    decimal? Maximum = null,
    bool AllowsStoreScope = true);

public static class SystemParameterCatalog
{
    public const string CopPerLoyaltyPoint = "crm.points.cop_per_point";
    public const string CopPerRedeemedPoint = "crm.points.cop_per_redeemed_point";
    public const string PqrsResponseDays = "crm.pqrs.response_days";
    public const string ReplenishmentConsumptionWindowDays =
        "inventory.replenishment.consumption_window_days";
    public const string ReplenishmentCoverageDays =
        "inventory.replenishment.coverage_days";
    public const string ContractExpiryAlertDays = "hr.contracts.expiry_alert_days";
    public const string MaxHoursPerShift = "hr.shifts.max_hours_per_shift";
    public const string MaxHoursPerDay = "hr.shifts.max_hours_per_day";

    public static readonly IReadOnlyList<ParameterDefinition> All =
        new[]
        {
            new ParameterDefinition(
                CopPerLoyaltyPoint,
                "Pesos por punto de fidelización",
                "Valor de compra en pesos que genera un punto de fidelización.",
                ParameterValueType.Decimal,
                "1000",
                "COP",
                Minimum: 1m,
                Maximum: 1_000_000m),
            new ParameterDefinition(
                CopPerRedeemedPoint,
                "Pesos por punto canjeado",
                "Descuento en pesos que representa un punto al momento del canje.",
                ParameterValueType.Decimal,
                "500",
                "COP",
                Minimum: 1m,
                Maximum: 1_000_000m),
            new ParameterDefinition(
                PqrsResponseDays,
                "Días hábiles de respuesta a PQRS",
                "Plazo en días hábiles para responder una petición, queja, reclamo o sugerencia.",
                ParameterValueType.Integer,
                "15",
                "días hábiles",
                Minimum: 1m,
                Maximum: 90m),
            new ParameterDefinition(
                ReplenishmentConsumptionWindowDays,
                "Ventana de consumo para reposición",
                "Días hacia atrás que se promedian para estimar el consumo diario de un producto.",
                ParameterValueType.Integer,
                "30",
                "días",
                Minimum: 1m,
                Maximum: 365m),
            new ParameterDefinition(
                ReplenishmentCoverageDays,
                "Días de cobertura objetivo",
                "Días de venta que debe cubrir el stock después de una reposición sugerida.",
                ParameterValueType.Integer,
                "15",
                "días",
                Minimum: 1m,
                Maximum: 365m),
            new ParameterDefinition(
                ContractExpiryAlertDays,
                "Días de aviso de vencimiento de contrato",
                "Antelación con la que se avisa que un contrato laboral está por vencer.",
                ParameterValueType.Integer,
                "30",
                "días",
                Minimum: 1m,
                Maximum: 180m),
            new ParameterDefinition(
                MaxHoursPerShift,
                "Máximo de horas por turno",
                "Duración máxima permitida para un turno de trabajo.",
                ParameterValueType.Decimal,
                "8",
                "horas",
                Minimum: 1m,
                Maximum: 24m),
            new ParameterDefinition(
                MaxHoursPerDay,
                "Máximo de horas por día",
                "Suma máxima de horas que un empleado puede tener asignadas en un mismo día.",
                ParameterValueType.Decimal,
                "8",
                "horas",
                Minimum: 1m,
                Maximum: 24m)
        };

    public static ParameterDefinition? Find(string key) =>
        All.FirstOrDefault(definition =>
            string.Equals(definition.Key, key?.Trim(), StringComparison.Ordinal));

    public static bool IsKnown(string key) => Find(key) is not null;
}
