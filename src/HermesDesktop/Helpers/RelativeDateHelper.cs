using System.Globalization;

namespace HermesDesktop.Helpers;

public static class RelativeDateHelper
{
    /// <summary>
    /// Converts a timestamp value (Unix epoch, ISO8601 string, or numeric string) to a relative "ago" string.
    /// </summary>
    public static string ToRelativeDate(object? value)
    {
        if (value == null) return "";

        DateTime? dt = ParseTimestamp(value);
        if (dt == null) return value.ToString() ?? "";

        return FormatRelative(dt.Value);
    }

    private static DateTime? ParseTimestamp(object value)
    {
        switch (value)
        {
            case DateTime d:
                return d;

            case double dbl:
                return FromUnixOrDirect(dbl);

            case long lng:
                return FromUnixOrDirect(lng);

            case int i:
                return FromUnixOrDirect(i);

            case string s:
                return ParseString(s);

            // System.Text.Json deserializes numbers as JsonElement
            case System.Text.Json.JsonElement je:
                return ParseJsonElement(je);

            default:
                return ParseString(value.ToString());
        }
    }

    private static DateTime? ParseJsonElement(System.Text.Json.JsonElement je)
    {
        if (je.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            if (je.TryGetDouble(out var d))
                return FromUnixOrDirect(d);
        }
        else if (je.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return ParseString(je.GetString());
        }
        return null;
    }

    private static DateTime? ParseString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        // Try numeric (Unix timestamp)
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return FromUnixOrDirect(num);

        // Try ISO8601
        s = s.Replace("Z", "+00:00");
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            return dto.LocalDateTime;

        return null;
    }

    private static DateTime FromUnixOrDirect(double value)
    {
        // Heuristic: if value > year 3000 in seconds, it's likely milliseconds
        if (value > 32503680000)
            return DateTimeOffset.FromUnixTimeMilliseconds((long)value).LocalDateTime;

        // If value looks like a reasonable Unix timestamp (after year 2000)
        if (value > 946684800)
            return DateTimeOffset.FromUnixTimeSeconds((long)value).LocalDateTime;

        return DateTime.MinValue;
    }

    private static string FormatRelative(DateTime dt)
    {
        if (dt == DateTime.MinValue) return "";

        var now = DateTime.Now;
        var diff = now - dt;

        if (diff.TotalSeconds < 0) return "just now";
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 2) return "1 minute ago";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} minutes ago";
        if (diff.TotalHours < 2) return "1 hour ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 2) return "yesterday";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} days ago";
        if (diff.TotalDays < 60) return "1 month ago";
        if (diff.TotalDays < 365) return $"{(int)(diff.TotalDays / 30)} months ago";
        return dt.ToString("yyyy-MM-dd");
    }
}
