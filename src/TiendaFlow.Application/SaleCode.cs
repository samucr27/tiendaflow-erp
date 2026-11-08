using System.Globalization;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Builds the human-readable sale code from the UTC instant. The UI shows
/// the same instant converted to Colombia time.
/// </summary>
public static class SaleCode
{
    public static string Create(DateTime date) =>
        $"VTA-{UtcTimestamp.Normalize(date).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}";
}
