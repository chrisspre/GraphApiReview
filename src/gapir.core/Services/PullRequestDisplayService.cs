namespace gapir.Services;

using System.Text.RegularExpressions;

[Obsolete("This class is obsolete. Use PullRequestRenderingService directly.")]
/// <summary>
/// Service responsible for displaying pull request information in formatted tables
/// </summary>
public partial class PullRequestDisplayService
{
    private readonly bool _showDetailedTiming;

    public PullRequestDisplayService(bool useShortUrls = true, bool showDetailedTiming = false)
    {
        _showDetailedTiming = showDetailedTiming;
    }

    /// <summary>
    /// Displays approved PRs in a simpler format
    /// </summary>
    public void DisplayApprovedPullRequestsTable(List<Models.PullRequestInfo> pullRequestInfos, Func<int, string> urlGenerator)
    {
        if (!pullRequestInfos.Any())
        {
            return;
        }

        Console.WriteLine("Reason why PR is not completed: ");
        Console.WriteLine("    REJ=Rejected, WFA=Waiting For Author, POL=Policy/Build Issues");
        Console.WriteLine("    PRA=Pending Reviewer Approval, POA=Pending Other Approvals");

        // Prepare table data - adjust headers based on detailed timing flag
        string[] headers;
        int[] maxWidths;

        if (_showDetailedTiming)
        {
            headers = new[] { "Author", "Title", "Why", "Age", "URL" };
            maxWidths = new[] { 20, 30, 3, 5, -1 }; // Adjusted widths for single age column
        }
        else
        {
            headers = new[] { "Author", "Title", "Why", "URL" };
            maxWidths = new[] { 25, 40, 8, -1 };
        }

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var url = urlGenerator(info.PullRequestId);
            var reason = info.PendingReason;

            if (_showDetailedTiming)
            {
                var age = FormatTimeDifferenceDays(DateTime.UtcNow - pr.CreationDate);
                rows.Add(new string[] { pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, age, url });
            }
            else
            {
                rows.Add(new string[] { pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, url });
            }
        }

        PrintTable(headers, rows, maxWidths);
    }

    /// <summary>
    /// Displays a formatted table of pull requests
    /// </summary>
    public void DisplayPullRequestsTable(List<Models.PullRequestInfo> pullRequestInfos, Func<int, string> urlGenerator)
    {
        if (!pullRequestInfos.Any())
        {
            Console.WriteLine("No pull requests found.");
            return;
        }

        // Prepare table data for pending PRs using original format
        var headers = new[] { "Title", "Status", "Author", "Age", "Ratio", "Change", "URL" };
        var maxWidths = new[] { 30, 6, 18, 10, 6, 20, -1 }; // -1 means no limit for URLs

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var cleanedTitle = ShortenTitle(pr.Title);
            var url = urlGenerator(info.PullRequestId);

            rows.Add(new string[]
            {
                cleanedTitle,
                info.MyVoteStatus,
                pr.CreatedBy.DisplayName,
                info.TimeAssigned,
                info.ApiApprovalRatio,
                info.LastChangeInfo,
                url
            });
        }

        PrintTable(headers, rows, maxWidths);
    }

    private static void PrintTable(string[] headers, IReadOnlyCollection<string[]> rows, int[] maxWidths)
    {
        if (headers == null || rows == null || maxWidths == null)
            return;

        if (headers.Length != maxWidths.Length)
            throw new ArgumentException("Headers and maxWidths arrays must have the same length.");

        // Calculate actual column widths based on content
        var actualWidths = new int[headers.Length];

        // Start with header lengths
        for (int i = 0; i < headers.Length; i++)
        {
            actualWidths[i] = maxWidths[i] == -1 ? headers[i].Length : Math.Min(headers[i].Length, maxWidths[i]);
        }

        // Check content lengths
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows

            for (int i = 0; i < row.Length && i < actualWidths.Length; i++)
            {
                var contentLength = row[i]?.Length ?? 0;
                if (maxWidths[i] == -1)
                {
                    // No limit for this column
                    actualWidths[i] = Math.Max(actualWidths[i], contentLength);
                }
                else
                {
                    actualWidths[i] = Math.Max(actualWidths[i], Math.Min(contentLength, maxWidths[i]));
                }
            }
        }

        // Print header
        Console.WriteLine();
        for (int i = 0; i < headers.Length; i++)
        {
            var header = maxWidths[i] == -1 ? headers[i] : TruncateString(headers[i], actualWidths[i]);
            Console.Write($"{header.PadRight(actualWidths[i])}");
            if (i < headers.Length - 1)
                Console.Write(" | ");
        }
        Console.WriteLine();

        // Print separator
        for (int i = 0; i < headers.Length; i++)
        {
            Console.Write(new string('-', actualWidths[i]));
            if (i < headers.Length - 1)
                Console.Write("-+-");
        }
        Console.WriteLine();

        // Print rows
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows

            for (int i = 0; i < row.Length; i++)
            {
                var content = maxWidths[i] == -1 ? (row[i] ?? "") : TruncateString(row[i] ?? "", actualWidths[i]);
                Console.Write($"{content.PadRight(actualWidths[i])}");
                if (i < row.Length - 1)
                    Console.Write(" | ");
            }
            Console.WriteLine();
        }
    }

    private static string FormatTimeDifferenceDays(TimeSpan timeDiff)
    {
        // For age columns, we only show days to keep it compact
        if (timeDiff.TotalDays >= 1)
            return $"{(int)timeDiff.TotalDays}d";
        else
            return "< 1d";
    }

    private static string ShortenTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return title;

        // Remove square bracket prefixes (e.g., "[xyz:SoA]", "[DirectoryServices]")
        var cleaned = SquareBracketPrefixRegex().Replace(title, "");

        // Remove all dashes and replace with spaces
        cleaned = cleaned.Replace("-", " ");

        // Remove trailing numbers (like issue numbers)
        // Pattern: removes numbers at the end, optionally preceded by spaces or special chars
        cleaned = NumbersPrefixRegex().Replace(cleaned, "");

        // Remove extra whitespace
        cleaned = SimplifySpacesRegex().Replace(cleaned, " ").Trim();

        // Truncate to 30 characters for table display
        if (cleaned.Length > 30)
        {
            cleaned = $"{cleaned[..27]}...";
        }

        return cleaned;
    }

    private static string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        if (input.Length <= maxLength)
            return input;

        return input.Substring(0, maxLength - 3) + "...";
    }

    [GeneratedRegex(@"^\s*\[[^\]]+\]\s*")]
    private static partial Regex SquareBracketPrefixRegex();
    
    [GeneratedRegex(@"^\s*\d+\s*")]
    private static partial Regex NumbersPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SimplifySpacesRegex();
}
