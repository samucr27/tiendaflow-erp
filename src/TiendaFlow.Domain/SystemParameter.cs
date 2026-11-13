using System.Globalization;

namespace TiendaFlow.Domain;

public enum ParameterValueType
{
    Integer = 1,
    Decimal = 2,
    Text = 3,
    Boolean = 4
}

public enum ParameterScope
{
    Global = 1,
    Store = 2
}

/// <summary>
/// One immutable version of a system parameter. Changing a parameter never
/// updates the previous row: it appends a new version with a higher number and
/// an effective date, so the history stays auditable and the latest applicable
/// version can be resolved by date.
/// </summary>
public class SystemParameter
{
    public SystemParameter(
        string key,
        ParameterValueType valueType,
        ParameterScope scope,
        int? storeId,
        string value,
        DateOnly effectiveFrom,
        int version,
        int? createdByUserId,
        DateTime createdAt)
    {
        RequireKey(key);
        RequireValueType(valueType);

        if (!Enum.IsDefined(scope))
            throw new InvalidParameterException("El alcance del parámetro no es válido.");
        if (scope == ParameterScope.Global && storeId is not null)
            throw new InvalidParameterException("Un parámetro global no puede tener tienda.");
        if (scope == ParameterScope.Store && storeId is not > 0)
            throw new InvalidParameterException("Un parámetro de tienda requiere una tienda válida.");
        if (version <= 0)
            throw new InvalidParameterException("La versión del parámetro debe ser positiva.");
        if (effectiveFrom == default)
            throw new InvalidParameterException("La fecha de vigencia es obligatoria.");
        if (createdAt == default)
            throw new InvalidParameterException("La fecha de creación es obligatoria.");

        Key = key.Trim();
        ValueType = valueType;
        Scope = scope;
        StoreId = storeId;
        Value = NormalizeValue(valueType, value);
        EffectiveFrom = effectiveFrom;
        Version = version;
        CreatedByUserId = createdByUserId;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public string Key { get; private set; }

    public ParameterValueType ValueType { get; private set; }

    public ParameterScope Scope { get; private set; }

    public int? StoreId { get; private set; }

    public string Value { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public int Version { get; private set; }

    public int? CreatedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Stores values in a canonical form (invariant numbers, lower-case
    /// booleans, trimmed text) so comparisons and reports never depend on how
    /// the user typed the value.
    /// </summary>
    public static string NormalizeValue(ParameterValueType valueType, string value)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length == 0)
            throw new InvalidParameterException("El valor del parámetro es obligatorio.");

        switch (valueType)
        {
            case ParameterValueType.Integer:
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    throw new InvalidParameterException("El valor debe ser un número entero.");
                return integer.ToString(CultureInfo.InvariantCulture);

            case ParameterValueType.Decimal:
                var normalized = NormalizeDecimalSeparators(text);
                if (!decimal.TryParse(
                        normalized,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var number))
                    throw new InvalidParameterException("El valor debe ser un número decimal.");
                return number.ToString(CultureInfo.InvariantCulture);

            case ParameterValueType.Boolean:
                if (bool.TryParse(text, out var boolean))
                    return boolean ? "true" : "false";

                var lowered = text.ToLowerInvariant();
                return lowered switch
                {
                    "si" or "sí" or "1" => "true",
                    "no" or "0" => "false",
                    _ => throw new InvalidParameterException("El valor debe ser verdadero o falso.")
                };

            case ParameterValueType.Text:
                if (text.Length > 500)
                    throw new InvalidParameterException("El texto no puede superar 500 caracteres.");
                return text;

            default:
                throw new InvalidParameterException("El tipo del parámetro no es válido.");
        }
    }

    /// <summary>
    /// Accepts the two ways a Colombian user types a decimal: "1500.25" or
    /// "1.500,25". When both separators appear, the dot is a thousands
    /// separator and the comma is the decimal mark.
    /// </summary>
    private static string NormalizeDecimalSeparators(string text)
    {
        if (text.Contains('.') && text.Contains(','))
            return text.Replace(".", string.Empty).Replace(',', '.');

        return text.Replace(',', '.');
    }

    private static void RequireKey(string key)    {
        var text = (key ?? string.Empty).Trim();

        if (text.Length is < 3 or > 120)
            throw new InvalidParameterException(
                "La clave del parámetro debe tener entre 3 y 120 caracteres.");

        foreach (var character in text)
        {
            var valid = char.IsAsciiLetterLower(character)
                || char.IsAsciiDigit(character)
                || character is '.' or '_' or '-';

            if (!valid)
                throw new InvalidParameterException(
                    "La clave solo admite minúsculas, dígitos, punto, guion y guion bajo.");
        }
    }

    private static void RequireValueType(ParameterValueType valueType)
    {
        if (!Enum.IsDefined(valueType))
            throw new InvalidParameterException("El tipo del parámetro no es válido.");
    }
}
