using TiendaFlow.Domain;

namespace TiendaFlow.Application;

/// <summary>
/// Converts UTC timestamps to the Colombian local time used by the screens.
/// Colombia has no daylight-saving changes, so the conversion is stable.
/// </summary>
public static class LocalTime
{
    private static readonly Lazy<TimeZoneInfo> Zone = new(ResolveZone);

    public static DateTime ToLocal(DateTime value) =>
        TimeZoneInfo.ConvertTimeFromUtc(UtcTimestamp.Normalize(value), Zone.Value);

    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "America/Bogota", "SA Pacific Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "TiendaFlow/Bogota",
            TimeSpan.FromHours(-5),
            "Hora de Colombia",
            "Hora de Colombia");
    }
}
