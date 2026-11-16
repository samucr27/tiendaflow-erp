using System.Globalization;
using TiendaFlow.Application;

namespace TiendaFlow.Web.Formatting;

/// <summary>
/// Formats values the way the Colombian stores expect: es-CO dates and
/// Colombian pesos. Keeping it in one place avoids locale drift between pages.
/// </summary>
public static class Formatters
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-CO");

    public static string Money(decimal value) => value.ToString("C0", Culture);

    public static string Number(decimal value) => value.ToString("N0", Culture);

    public static string LongDate(DateTime value) => Capitalize(
        LocalTime.ToLocal(value).ToString("dddd, d 'de' MMMM 'de' yyyy", Culture));

    public static string ShortDate(DateTime value) =>
        LocalTime.ToLocal(value).ToString("dd/MM/yyyy", Culture);

    public static string ShortDateTime(DateTime value) =>
        LocalTime.ToLocal(value).ToString("dd/MM/yyyy HH:mm", Culture);

    public static string Quantity(int value) => value.ToString("N0", Culture);

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpper(value[0], Culture) + value[1..];
}
