using System.Globalization;
using TiendaFlow.Domain;

namespace TiendaFlow.Web.Formatting;

/// <summary>
/// Labels for the parameter screen. Values are rendered with the Colombian
/// culture, while the database keeps the canonical invariant form.
/// </summary>
public static class ParameterText
{
    public static string Type(ParameterValueType type) => type switch
    {
        ParameterValueType.Integer => "Entero",
        ParameterValueType.Decimal => "Decimal",
        ParameterValueType.Text => "Texto",
        ParameterValueType.Boolean => "Booleano",
        _ => type.ToString()
    };

    public static string Scope(ParameterScope scope) =>
        scope == ParameterScope.Global ? "Global" : "Tienda";

    public static string Value(ParameterValueType type, string value)
    {
        switch (type)
        {
            case ParameterValueType.Boolean:
                return value == "true" ? "Sí" : "No";

            case ParameterValueType.Integer:
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                    ? integer.ToString("N0", Formatters.Culture)
                    : value;

            case ParameterValueType.Decimal:
                return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                    ? number.ToString("N2", Formatters.Culture)
                    : value;

            default:
                return value;
        }
    }
}
