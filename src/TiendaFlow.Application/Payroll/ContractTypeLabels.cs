using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

/// <summary>
/// Translates the contract type enum to the Spanish labels the employees are
/// registered with. The employee table stores the label as free text, so the
/// parser accepts the label, common unaccented variants and the enum name.
/// </summary>
public static class ContractTypeLabels
{
    private static readonly IReadOnlyDictionary<ContractType, string> Labels =
        new Dictionary<ContractType, string>
        {
            [ContractType.Indefinite] = "Indefinido",
            [ContractType.FixedTerm] = "Término fijo",
            [ContractType.WorkOrService] = "Obra o labor",
            [ContractType.ServiceProvision] = "Prestación de servicios"
        };

    private static readonly IReadOnlyDictionary<string, ContractType> Aliases = BuildAliases();

    public static IReadOnlyList<ContractType> All { get; } = new[]
    {
        ContractType.Indefinite,
        ContractType.FixedTerm,
        ContractType.WorkOrService,
        ContractType.ServiceProvision
    };

    public static string ToLabel(ContractType contractType) =>
        Labels.TryGetValue(contractType, out var label) ? label : contractType.ToString();

    /// <summary>
    /// Returns the Spanish label for a stored value. Unknown values are
    /// returned unchanged so the screen can show what is actually stored.
    /// </summary>
    public static string ToLabel(string value) =>
        TryParse(value, out var contractType) ? ToLabel(contractType) : value.Trim();

    public static bool IsKnown(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out ContractType contractType)
    {
        contractType = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = Normalize(value);

        if (Aliases.TryGetValue(normalized, out contractType))
            return true;

        return Enum.TryParse(normalized, ignoreCase: true, out contractType)
            && Enum.IsDefined(contractType);
    }

    private static string Normalize(string value) => string.Join(
        ' ',
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static IReadOnlyDictionary<string, ContractType> BuildAliases()
    {
        var aliases = new Dictionary<string, ContractType>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in Labels)
            aliases[Normalize(pair.Value)] = pair.Key;

        // The employee form and the seed data may contain the label without
        // accents; accepting it avoids forcing a correction just to calculate.
        aliases["Termino fijo"] = ContractType.FixedTerm;
        aliases["Prestacion de servicios"] = ContractType.ServiceProvision;

        return aliases;
    }
}
