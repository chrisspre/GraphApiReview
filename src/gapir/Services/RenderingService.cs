namespace gapir.Services;

using gapir.Extensions;
using gapir.Models;
using System.Text.RegularExpressions;

public partial class RenderingService(TerminalLinkService terminalLinkService)
{
    private readonly TerminalLinkService _terminalLinkService = terminalLinkService;

    /// <summary>
    /// Renders pending pull requests result
    /// </summary>
    public void RenderPendingPullRequests(PendingPullRequestResult result, PullRequestRenderingOptions options)
    {
        RenderPendingText(result, options);
    }

    /// <summary>
    /// Renders approved pull requests result
    /// </summary>
    public void RenderApprovedPullRequests(ApprovedPullRequestResult result, PullRequestRenderingOptions options)
    {
        RenderApprovedText(result, options);
    }

    /// <summary>
    /// Renders completed pull requests result
    /// </summary>
    public void RenderCompletedPullRequests(CompletedPullRequestResult result, PullRequestRenderingOptions options)
    {
        RenderCompletedText(result, options);
    }

    /// <summary>
    /// Renders a report of approved completed PRs grouped by week
    /// </summary>
    public void RenderReportPullRequests(List<gapir.Models.PullRequestInfo> approvedPRs, string currentUserDisplayName, int weeks, PullRequestRenderingOptions options)
    {
        RenderReportText(approvedPRs, currentUserDisplayName, weeks);
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

    private void RenderCompletedText(CompletedPullRequestResult result, PullRequestRenderingOptions options)
    {
        Console.WriteLine($"gapir (Graph API Review) - Completed Pull Requests (Last {result.DaysBack} Days)");
        Console.WriteLine("===================================================================");
        Console.WriteLine();

        var completedPRs = result.CompletedPullRequests.ToList();
        
        Console.WriteLine($"✅ {completedPRs.Count} completed PR(s) where {result.CurrentUserDisplayName} was reviewer (last {result.DaysBack} days):");
        
        if (completedPRs.Any())
        {
            RenderCompletedPRsTable(completedPRs, options);
        }
        else
        {
            Console.WriteLine($"No completed pull requests found in the last {result.DaysBack} days.");
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

    private void RenderReportText(List<gapir.Models.PullRequestInfo> approvedPRs, string currentUserDisplayName, int weeks)
    {
        var weekLabel = weeks == 1 ? "Last Week" : $"Last {weeks} Weeks";
        Console.WriteLine($"gapir (Graph API Review) - Review Report ({weekLabel})");
        Console.WriteLine("===================================================================");
        Console.WriteLine();

        if (!approvedPRs.Any())
        {
            Console.WriteLine($"No approved completed pull requests found ({weekLabel.ToLower()}).");
            return;
        }

        Console.WriteLine($"{approvedPRs.Count} PR(s) approved by {currentUserDisplayName} ({weekLabel.ToLower()}):");

        var grouped = GroupByWeek(approvedPRs);

        foreach (var week in grouped)
        {
            var weekEnd = week.Key.AddDays(6);
            Console.WriteLine();
            Console.WriteLine($"  Week of {week.Key:MMM dd} - {weekEnd:MMM dd, yyyy} ({week.Count()} PRs)");
            Console.WriteLine($"  {new string('-', 50)}");

            var headers = new[] { "Title", "Author", "Opened", "Closed", "Review" };
            var maxWidths = new[] { 50, 20, 8, 8, 12 };
            var alignRight = new[] { false, false, false, false, true };

            var rows = new List<string[]>();
            foreach (var info in week.OrderByDescending(pr => pr.PullRequest.ClosedDate))
            {
                var pr = info.PullRequest;
                var clickableTitle = _terminalLinkService.CreatePullRequestLink(info.PullRequestId, ShortenTitle(pr.Title));
                var openedStr = pr.CreationDate.ToString("MMM dd");
                var closedStr = pr.ClosedDate.ToString("MMM dd");
                var reviewTime = GetReviewTimeDisplay(info);

                rows.Add([clickableTitle, pr.CreatedBy.DisplayName, openedStr, closedStr, reviewTime]);
            }

            PrintTable(headers, rows, maxWidths, alignRight);
        }

        Console.WriteLine($"  Total: {approvedPRs.Count} PRs approved across {grouped.Count()} weeks");
    }

    private static IOrderedEnumerable<IGrouping<DateTime, gapir.Models.PullRequestInfo>> GroupByWeek(List<gapir.Models.PullRequestInfo> pullRequests)
    {
        return pullRequests
            .GroupBy(pr =>
            {
                var closedDate = pr.PullRequest.ClosedDate;
                // Get the Monday of the week
                var daysFromMonday = ((int)closedDate.DayOfWeek + 6) % 7;
                return closedDate.AddDays(-daysFromMonday).Date;
            })
            .OrderByDescending(g => g.Key);
    }

    /// <summary>
    /// Gets the review time span: from assigned to voted if available, otherwise assigned to closed.
    /// Returns null if no assignment date is available.
    /// </summary>
    private static TimeSpan? GetReviewTimeSpan(gapir.Models.PullRequestInfo info)
    {
        if (info.ReviewerAssignedDate == null || info.ReviewerVotedDate == null)
        {
            return null;
        }

        return info.ReviewerVotedDate.Value - info.ReviewerAssignedDate.Value;
    }

    private static string GetReviewTimeDisplay(gapir.Models.PullRequestInfo info)
    {
        var span = GetReviewTimeSpan(info);
        if (span == null)
        {
            return "n/a";
        }

        return DateTimeExtensions.FormatDuration(span.Value);
    }

    private void RenderApprovedPRsTable(List<gapir.Models.PullRequestInfo> approvedPRs, PullRequestRenderingOptions options)
    {
        DisplayApprovedPullRequestsTable(approvedPRs, options.ShowDetailedTiming);
    }

    private void RenderCompletedPRsTable(List<gapir.Models.PullRequestInfo> completedPRs, PullRequestRenderingOptions options)
    {
        DisplayCompletedPullRequestsTable(completedPRs, options.ShowDetailedTiming);
    }

    private void RenderPendingPRsTable(List<gapir.Models.PullRequestInfo> pendingPRs, PullRequestRenderingOptions options)
    {
        DisplayPullRequestsTable(pendingPRs);
    }

    /// <summary>
    /// Displays approved PRs in a simpler format with clickable titles
    /// </summary>
    private void DisplayApprovedPullRequestsTable(List<gapir.Models.PullRequestInfo> pullRequestInfos, bool showDetailedTiming)
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
            headers = new[] { "Title", "Author", "Why", "Reviewers" };
            maxWidths = new[] { 50, 20, 8, 25 }; // Standardized Title=50, Author=20 for consistency
        }
        else
        {
            headers = new[] { "Title", "Author", "Why" };
            maxWidths = new[] { 50, 20, 8 }; // Standardized Title=50, Author=20 for consistency
        }

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var reason = info.PendingReason;
            var clickableTitle = _terminalLinkService.CreatePullRequestLink(info.PullRequestId, ShortenTitle(pr.Title));

            if (showDetailedTiming)
            {
                rows.Add(new string[] { clickableTitle, pr.CreatedBy.DisplayName, reason, info.TimeAssigned });
            }
            else
            {
                rows.Add(new string[] { clickableTitle, pr.CreatedBy.DisplayName, reason });
            }
        }

        PrintTable(headers, rows, maxWidths);
    }

    /// <summary>
    /// Displays a formatted table of pull requests with clickable titles
    /// </summary>
    private void DisplayPullRequestsTable(List<gapir.Models.PullRequestInfo> pullRequestInfos)
    {
        if (!pullRequestInfos.Any())
        {
            Console.WriteLine("No pull requests found.");
            return;
        }

        // Prepare table data for pending PRs (no URL column)
        var headers = new[] { "Title", "Author", "Status", "Age", "X/Y", "Change" };
        var maxWidths = new[] { 50, 20, 6, 10, 6, 27 }; // Standardized Title=50, Author=20 for consistency across tables

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var clickableTitle = _terminalLinkService.CreatePullRequestLink(info.PullRequestId, ShortenTitle(pr.Title));

            rows.Add(new string[]
            {
                clickableTitle,
                pr.CreatedBy.DisplayName,
                info.MyVoteStatus,
                info.TimeAssigned,
                info.ApiApprovalRatio,
                info.LastChangeInfo
            });
        }

        PrintTable(headers, rows, maxWidths);
    }

    /// <summary>
    /// Displays completed PRs with completion info and user's vote
    /// </summary>
    private void DisplayCompletedPullRequestsTable(List<gapir.Models.PullRequestInfo> pullRequestInfos, bool showDetailedTiming)
    {
        if (!pullRequestInfos.Any())
        {
            return;
        }

        Console.WriteLine("Completed pull requests where you were a reviewer:");

        // Prepare table data
        string[] headers;
        int[] maxWidths;

        if (showDetailedTiming)
        {
            headers = new[] { "Title", "Author", "Vote", "Assigned", "Completed" };
            maxWidths = new[] { 50, 20, 8, 12, 12 };
        }
        else
        {
            headers = new[] { "Title", "Author", "Vote", "Completed" };
            maxWidths = new[] { 50, 20, 10, 12 };
        }

        var rows = new List<string[]>();
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var clickableTitle = _terminalLinkService.CreatePullRequestLink(info.PullRequestId, ShortenTitle(pr.Title));
            var voteStatus = GetVoteDisplayText(info.MyVoteStatus);
            var completedDate = pr.ClosedDate.FormatRelativeTime();
            
            if (showDetailedTiming)
            {
                var assignedDate = pr.CreationDate.FormatRelativeTime();
                rows.Add(new string[] { clickableTitle, pr.CreatedBy.DisplayName, voteStatus, assignedDate, completedDate });
            }
            else
            {
                rows.Add(new string[] { clickableTitle, pr.CreatedBy.DisplayName, voteStatus, completedDate });
            }
        }

        PrintTable(headers, rows, maxWidths);
    }

    private static string GetVoteDisplayText(string voteStatus)
    {
        return voteStatus switch
        {
            "10" => "Approved",
            "5" => "ApproveS",  // Approved with suggestions
            "Reject" => "Reject",
            "0" => "NoVote",
            "---" => "NotReq",  // Not required
            _ => voteStatus
        };
    }



    private static void PrintTable(string[] headers, IReadOnlyCollection<string[]> rows, int[] maxWidths, bool[]? rightAlign = null)
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
            var isRight = rightAlign != null && i < rightAlign.Length && rightAlign[i];
            Console.Write(isRight ? PadLeftToVisualWidth(header, actualWidths[i]) : PadToVisualWidth(header, actualWidths[i]));
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
                var isRight = rightAlign != null && i < rightAlign.Length && rightAlign[i];
                Console.Write(isRight ? PadLeftToVisualWidth(content, actualWidths[i]) : PadToVisualWidth(content, actualWidths[i]));
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

    private static string PadLeftToVisualWidth(string text, int targetWidth)
    {
        if (string.IsNullOrEmpty(text))
            return new string(' ', targetWidth);

        var visualWidth = GetVisualWidth(text);
        if (visualWidth >= targetWidth)
            return text;

        return new string(' ', targetWidth - visualWidth) + text;
    }
}
