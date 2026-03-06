namespace PicoBusX.Web.Formatting;

public static class EntityDisplayFormatter
{
    public static string FormatSize(long bytes)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        if (bytes < 1024 * 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024):F1} MB";
        }

        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    public static string FormatDate(DateTimeOffset? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss zzz") : "—";
    }

    public static string FormatTimeSpan(TimeSpan value)
    {
        if (value == TimeSpan.MaxValue)
        {
            return "Never";
        }

        if (value == TimeSpan.Zero)
        {
            return "0 seconds";
        }

        var parts = new List<string>();

        if (value.Days > 0)
        {
            parts.Add($"{value.Days} day{(value.Days == 1 ? string.Empty : "s")}");
        }

        if (value.Hours > 0)
        {
            parts.Add($"{value.Hours} hour{(value.Hours == 1 ? string.Empty : "s")}");
        }

        if (value.Minutes > 0)
        {
            parts.Add($"{value.Minutes} minute{(value.Minutes == 1 ? string.Empty : "s")}");
        }

        if (value.Seconds > 0)
        {
            parts.Add($"{value.Seconds} second{(value.Seconds == 1 ? string.Empty : "s")}");
        }

        if (parts.Count == 0)
        {
            parts.Add($"{Math.Max(1, (int)Math.Round(value.TotalMilliseconds))} ms");
        }

        return string.Join(" ", parts);
    }

    public static string FormatText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    public static string FormatBoolean(bool value)
    {
        return value ? "Yes" : "No";
    }

    public static string FormatMegabytes(long value)
    {
        return value > 0 ? $"{value} MB" : "—";
    }
}

