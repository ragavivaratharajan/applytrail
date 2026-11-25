using System.Text;

namespace ApplyTrail.Application.Logic;

/// <summary>
/// Minimal RFC 4180 writer with one extra safeguard: a cell that starts with = + - @ (or a tab / carriage
/// return) is prefixed with an apostrophe so Excel and Sheets show it as text instead of running it as a
/// formula. Job titles and notes are user-typed or pasted from the web, so this is not hypothetical.
/// </summary>
public static class CsvExporter
{
    private const string NewLine = "\r\n";

    public static string Write(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        AppendRow(builder, headers.Select(h => (string?)h));

        foreach (var row in rows)
        {
            AppendRow(builder, row);
        }

        return builder.ToString();
    }

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var text = value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;

        var needsQuotes = text.AsSpan().IndexOfAny(",\"\r\n") >= 0;
        return needsQuotes ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string?> cells)
    {
        builder.AppendJoin(',', cells.Select(Escape));
        builder.Append(NewLine);
    }
}
