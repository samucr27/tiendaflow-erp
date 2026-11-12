using System.Globalization;
using System.Text;

namespace TiendaFlow.Application.Reporting;

/// <summary>
/// Minimal RFC 4180 writer. Values containing a comma, a quote or a line break
/// are quoted and inner quotes are doubled, so a product name with a comma or
/// a customer name with quotes never breaks the file.
/// </summary>
public static class CsvReport
{
    public static string Build(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();

        AppendLine(builder, headers);

        foreach (var row in rows)
            AppendLine(builder, row);

        return builder.ToString();
    }

    public static string Number(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Date(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static void AppendLine(StringBuilder builder, IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
                builder.Append(',');

            builder.Append(Escape(values[index]));
        }

        builder.Append("\r\n");
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;

        if (!value.Contains(',')
            && !value.Contains('"')
            && !value.Contains('\n')
            && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
