namespace gapir.Services;

using gapir.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

public partial class PullRequestRenderingService
{
    private readonly UrlGeneratorService _urlGenerator;

    public PullRequestRenderingService(UrlGeneratorService urlGenerator)
    {
        _urlGenerator = urlGenerator;
    }

    /// <summary>
    /// Renders pending pull requests result
    /// </summary>
    public void RenderPendingPullRequests(PendingPullRequestResult result, PullRequestRenderingOptions options)
    {
        if (options.Format == Format.Json)
        {
            RenderPendingJson(result);
        }
        else
        {
            RenderPendingText(result, options);
        }
    }

    /// <summary>
    /// Renders approved pull requests result
    /// </summary>
    public void RenderApprovedPullRequests(ApprovedPullRequestResult result, PullRequestRenderingOptions options)
    {
        if (options.Format == Format.Json)
        {
            RenderApprovedJson(result);
        }
        else
        {
            RenderApprovedText(result, options);
        }
    }

    private void RenderPendingJson(PendingPullRequestResult result)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(new
        {
            Type = "PendingPullRequests",
            User = result.CurrentUserDisplayName,
            Statistics = result.Statistics,
            PullRequests = result.PendingPullRequests
        }, jsonOptions);
        
        Console.WriteLine(json);
    }

    private void RenderApprovedJson(ApprovedPullRequestResult result)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(new
        {
            Type = "ApprovedPullRequests",
            User = result.CurrentUserDisplayName,
            Statistics = result.Statistics,
            PullRequests = result.ApprovedPullRequests
        }, jsonOptions);
        
        Console.WriteLine(json);
    }

    private void RenderPendingText(PendingPullRequestResult result, PullRequestRenderingOptions options)
    {
        Console.WriteLine("gapir (Graph API Review) - Pull Requests Pending Review");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        var pendingPRs = result.PendingPullRequests.ToList();
        
        Console.WriteLine($"⏳ {pendingPRs.Count} incomplete PR(s) assigned to {result.CurrentUserDisplayName}:");
        
        if (pendingPRs.Any())
        {
            RenderPendingPRsTable(pendingPRs, options);
        }
        else
        {
            Console.WriteLine("No pull requests found requiring your review.");
        }

#if ENABLE_STATISTICS
        // Show statistics
        RenderStatistics(result.Statistics);
#endif
    }

    private void RenderApprovedText(ApprovedPullRequestResult result, PullRequestRenderingOptions options)
    {
        Console.WriteLine("gapir (Graph API Review) - Already Approved Pull Requests");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        var approvedPRs = result.ApprovedPullRequests.ToList();
        
        Console.WriteLine($"✓ {approvedPRs.Count} PR(s) already approved by {result.CurrentUserDisplayName}:");
        
        if (approvedPRs.Any())
        {
            RenderApprovedPRsTable(approvedPRs, options);
        }
        else
        {
            Console.WriteLine("No approved pull requests found.");
        }

#if ENABLE_STATISTICS
        // Show statistics
        RenderStatistics(result.Statistics);
#endif
    }

#if ENABLE_STATISTICS
    private void RenderStatistics(PullRequestStatistics stats)
    {
        Console.WriteLine();
        Console.WriteLine("Statistics:");
        Console.WriteLine($"  Total Assigned: {stats.TotalAssigned}");
        Console.WriteLine($"  Pending Review: {stats.PendingReview}");
        Console.WriteLine($"  Already Approved: {stats.AlreadyApproved}");
    }
#endif

    private void RenderApprovedPRsTable(List<PullRequestInfo> approvedPRs, PullRequestRenderingOptions options)
    {
        DisplayApprovedPullRequestsTable(approvedPRs, id => _urlGenerator.GenerateUrl(id, options.UseShortUrls), options.ShowDetailedTiming);
    }

    private void RenderPendingPRsTable(List<PullRequestInfo> pendingPRs, PullRequestRenderingOptions options)
    {
        DisplayPullRequestsTable(pendingPRs, id => _urlGenerator.GenerateUrl(id, options.UseShortUrls));
    }

    /// <summary>
    /// Displays approved PRs in a simpler format
    /// </summary>
    private void DisplayApprovedPullRequestsTable(List<PullRequestInfo> pullRequestInfos, Func<int, string> urlGenerator, bool showDetailedTiming)
    {
        if (!pullRequestInfos.Any())
        {
            return;
        }

        Console.WriteLine("Reason why PR is not completed: ");
        Console.WriteLine("    Reject=Rejected, Wait4A=Waiting For Author, Policy=Policy/Build Issues");
        Console.WriteLine("    PendRv=Pending Reviewer Approval, PendOt=Pending Other Approvals");

        // Prepare table data - adjust headers based on detailed timing flag
        string[] headers;
        int[] maxWidths;

        if (showDetailedTiming)
        {
            headers = new[] { "Author", "Title", "Why", "Reviewers", "URL" };
            maxWidths = new[] { 20, 30, 8, 25, -1 }; // Increased Why column from 3 to 8 to show full reason codes
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

            if (showDetailedTiming)
            {
                rows.Add(new string[] { pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, info.TimeAssigned, url });
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
    private void DisplayPullRequestsTable(List<PullRequestInfo> pullRequestInfos, Func<int, string> urlGenerator)
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
