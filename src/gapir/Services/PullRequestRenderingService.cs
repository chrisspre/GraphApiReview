namespace gapir.Services;

using gapir.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

public partial class PullRequestRenderingService
{
    public PullRequestRenderingService()
    {
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
        DisplayApprovedPullRequestsTable(approvedPRs, options.ShowDetailedTiming);
    }

    private void RenderPendingPRsTable(List<PullRequestInfo> pendingPRs, PullRequestRenderingOptions options)
    {
        DisplayPullRequestsTable(pendingPRs);
    }

    /// <summary>
    /// Displays approved PRs in a simpler format with clickable titles
    /// </summary>
    private void DisplayApprovedPullRequestsTable(List<PullRequestInfo> pullRequestInfos, bool showDetailedTiming)
    {
        if (!pullRequestInfos.Any())
        {
            return;
        }

        Console.WriteLine("Reason why PR is not completed: ");
        Console.WriteLine("    Reject=Rejected, Wait4A=Waiting For Author, Policy=Policy/Build Issues");
        Console.WriteLine("    PendRv=Pending Reviewer Approval, PendOt=Pending Other Approvals");

        // Prepare table data - adjust headers based on detailed timing flag (no URL column)
        string[] headers;
        int[] maxWidths;

        if (showDetailedTiming)
        {
            headers = new[] { "Author", "Title", "Why", "Reviewers" };
            maxWidths = new[] { 20, 65, 8, 25 }; // Increased title column for 120 char table width
        }
        else
        {
            headers = new[] { "Author", "Title", "Why" };
            maxWidths = new[] { 25, 75, 8 }; // Increased title column for 120 char table width
        }

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var reason = info.PendingReason;
            var clickableTitle = TerminalLinkService.CreatePullRequestLink(info.PullRequestId, ShortenTitle(pr.Title));

            if (showDetailedTiming)
            {
                rows.Add(new string[] { pr.CreatedBy.DisplayName, clickableTitle, reason, info.TimeAssigned });
            }
            else
            {
                rows.Add(new string[] { pr.CreatedBy.DisplayName, clickableTitle, reason });
            }
        }

        PrintTable(headers, rows, maxWidths);
    }

    /// <summary>
    /// Displays a formatted table of pull requests with clickable titles
    /// </summary>
    private void DisplayPullRequestsTable(List<PullRequestInfo> pullRequestInfos)
    {
        if (!pullRequestInfos.Any())
        {
            Console.WriteLine("No pull requests found.");
            return;
        }

        // Prepare table data for pending PRs (no URL column)
        var headers = new[] { "Title", "Status", "Author", "Age", "Ratio", "Change" };
        var maxWidths = new[] { 55, 6, 18, 10, 6, 20 }; // Increased title column for 120 char table width

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var clickableTitle = TerminalLinkService.CreatePullRequestLink(info.PullRequestId, ShortenTitle(pr.Title));

            rows.Add(new string[]
            {
                clickableTitle,
                info.MyVoteStatus,
                pr.CreatedBy.DisplayName,
                info.TimeAssigned,
                info.ApiApprovalRatio,
                info.LastChangeInfo
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

        // Start with header lengths (using visual width)
        for (int i = 0; i < headers.Length; i++)
        {
            var headerWidth = GetVisualWidth(headers[i]);
            actualWidths[i] = maxWidths[i] == -1 ? headerWidth : Math.Min(headerWidth, maxWidths[i]);
        }

        // Check content lengths (using visual width)
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows

            for (int i = 0; i < row.Length && i < actualWidths.Length; i++)
            {
                var contentLength = GetVisualWidth(row[i] ?? "");
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
            var header = maxWidths[i] == -1 ? headers[i] : TruncateStringByVisualWidth(headers[i], actualWidths[i]);
            Console.Write(PadToVisualWidth(header, actualWidths[i]));
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
                var content = maxWidths[i] == -1 ? (row[i] ?? "") : TruncateStringByVisualWidth(row[i] ?? "", actualWidths[i]);
                Console.Write(PadToVisualWidth(content, actualWidths[i]));
                if (i < row.Length - 1)
                    Console.Write(" | ");
            }
            Console.WriteLine();
        }
        
        // Add empty line after table
        Console.WriteLine();
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

    /// <summary>
    /// Truncates a string based on visual width, preserving escape sequences
    /// </summary>
    private static string TruncateStringByVisualWidth(string input, int maxVisualWidth)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var visualWidth = GetVisualWidth(input);
        if (visualWidth <= maxVisualWidth)
            return input;

        // For strings with escape sequences, we need to be more careful about truncation
        // For now, if it contains escape sequences and is too long, truncate by removing characters from the end
        if (AnsiEscapeRegex().IsMatch(input))
        {
            // This is a simplified approach - in a real implementation you might want to
            // preserve escape sequences and only truncate the visible text
            var cleanText = AnsiEscapeRegex().Replace(input, "");
            if (cleanText.Length > maxVisualWidth - 3)
            {
                var truncatedClean = cleanText.Substring(0, maxVisualWidth - 3) + "...";
                // For hyperlinks, we'll just return the truncated clean text 
                // since preserving partial escape sequences could break the link
                return truncatedClean;
            }
            return input;
        }

        // No escape sequences, use regular truncation
        return input.Substring(0, maxVisualWidth - 3) + "...";
    }

    [GeneratedRegex(@"^\s*\[[^\]]+\]\s*")]
    private static partial Regex SquareBracketPrefixRegex();
    
    [GeneratedRegex(@"^\s*\d+\s*")]
    private static partial Regex NumbersPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SimplifySpacesRegex();

    [GeneratedRegex(@"\u001b\[[0-9;]*[a-zA-Z]|\u001b\]8;;[^\u0007\u001b]*\u001b\\|\u001b\]8;;\u001b\\")]
    private static partial Regex AnsiEscapeRegex();

    /// <summary>
    /// Calculates the visual width of a string, excluding ANSI escape sequences and OSC 8 hyperlinks
    /// </summary>
    private static int GetVisualWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Remove ANSI escape sequences and OSC 8 hyperlinks to get actual display width
        var cleanText = AnsiEscapeRegex().Replace(text, "");
        return cleanText.Length;
    }

    /// <summary>
    /// Pads a string to the specified visual width, accounting for escape sequences
    /// </summary>
    private static string PadToVisualWidth(string text, int targetWidth)
    {
        if (string.IsNullOrEmpty(text))
            return new string(' ', targetWidth);

        var visualWidth = GetVisualWidth(text);
        if (visualWidth >= targetWidth)
            return text;

        // Add padding to reach target visual width
        return text + new string(' ', targetWidth - visualWidth);
    }
}
