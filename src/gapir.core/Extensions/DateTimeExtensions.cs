namespace gapir.Extensions;

/// <summary>
/// Extension methods for DateTime formatting
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Formats a date as a relative time string with smart unit selection
    /// Examples: "5m ago", "2h ago", "3d ago", "2w ago", "4mo ago"
    /// </summary>
    public static string FormatRelativeTime(this DateTime date)
    {
        var timeDiff = DateTime.UtcNow - date;
        return FormatTimeSpan(timeDiff);
    }

    /// <summary>
    /// Formats a TimeSpan as a relative time string with smart unit selection
    /// Examples: "5m ago", "2h ago", "3d ago", "2w ago", "4mo ago"
    /// </summary>
    public static string FormatTimeSpan(TimeSpan timeDiff)
    {
        // Handle negative time spans (future dates)
        if (timeDiff.TotalSeconds < 0)
            return "future";

        // Less than 1 minute
        if (timeDiff.TotalMinutes < 1)
            return "just now";

        // Less than 1 hour: show minutes
        if (timeDiff.TotalHours < 1)
            return $"{(int)timeDiff.TotalMinutes}m ago";

        // Less than 1 day: show hours
        if (timeDiff.TotalDays < 1)
            return $"{(int)timeDiff.TotalHours}h ago";

        // Less than 2 weeks: show days
        if (timeDiff.TotalDays < 14)
            return $"{(int)timeDiff.TotalDays}d ago";

        // Less than 8 weeks: show weeks
        if (timeDiff.TotalDays < 56) // 8 weeks
        {
            var weeks = (int)(timeDiff.TotalDays / 7);
            return $"{weeks}w ago";
        }

        // 8 weeks or more: show months
        var months = (int)(timeDiff.TotalDays / 30.44); // Average days per month
        return $"{months}mo ago";
    }
}