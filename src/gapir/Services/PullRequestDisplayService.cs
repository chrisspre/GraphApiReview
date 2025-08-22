namespace gapir.Services;

/// <summary>
/// Service responsible for displaying pull request information in formatted tables
/// </summary>
public class PullRequestDisplayService
{
    private readonly bool _useShortUrls;

    public PullRequestDisplayService(bool useShortUrls = true)
    {
        _useShortUrls = useShortUrls;
    }

    /// <summary>
    /// Displays a formatted table of pull requests
    /// </summary>
    public void DisplayPullRequestsTable(List<Models.PullRequestInfo> pullRequestInfos)
    {
        if (!pullRequestInfos.Any())
        {
            Console.WriteLine("No pull requests found.");
            return;
        }

        // Define column widths
        const int idWidth = 7;
        const int titleWidth = 50;
        const int statusWidth = 8;
        const int myVoteWidth = 7;
        const int ratioWidth = 6;
        const int ageWidth = 6;
        const int reasonWidth = 7;
        const int changeWidth = 20;
        const int urlWidth = 25;

        // Print header
        Console.WriteLine();
        Console.WriteLine($"{"ID",-idWidth} {"Title",-titleWidth} {"Status",-statusWidth} {"MyVote",-myVoteWidth} {"Ratio",-ratioWidth} {"Age",-ageWidth} {"Reason",-reasonWidth} {"Change",-changeWidth} {"URL",-urlWidth}");
        Console.WriteLine(new string('=', idWidth + titleWidth + statusWidth + myVoteWidth + ratioWidth + ageWidth + reasonWidth + changeWidth + urlWidth + 8));

        // Print each pull request
        foreach (var info in pullRequestInfos)
        {
            var pr = info.PullRequest;
            var truncatedTitle = TruncateString(pr.Title, titleWidth);
            var status = FormatStatus(pr.Status);
            var url = _useShortUrls ? info.ShortUrl : info.FullUrl;

            Console.WriteLine($"{pr.PullRequestId,-idWidth} {truncatedTitle,-titleWidth} {status,-statusWidth} {info.MyVoteStatus,-myVoteWidth} {info.ApiApprovalRatio,-ratioWidth} {info.TimeAssigned,-ageWidth} {info.PendingReason,-reasonWidth} {info.LastChangeInfo,-changeWidth} {url,-urlWidth}");
        }

        Console.WriteLine();
        Console.WriteLine($"Total: {pullRequestInfos.Count} pull requests");
    }

    /// <summary>
    /// Displays a summary of pull request statistics
    /// </summary>
    public void DisplaySummary(List<Models.PullRequestInfo> pullRequestInfos)
    {
        Console.WriteLine("\n=== SUMMARY ===");
        Console.WriteLine($"Total PRs: {pullRequestInfos.Count}");
        Console.WriteLine($"Approved by me: {pullRequestInfos.Count(info => info.IsApprovedByMe)}");
        Console.WriteLine($"Not yet approved by me: {pullRequestInfos.Count(info => !info.IsApprovedByMe)}");
        
        // Group by pending reason
        var reasonGroups = pullRequestInfos.GroupBy(info => info.PendingReason);
        Console.WriteLine("\nBy pending reason:");
        foreach (var group in reasonGroups.OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        }

        // Group by my vote status
        var voteGroups = pullRequestInfos.GroupBy(info => info.MyVoteStatus);
        Console.WriteLine("\nBy my vote status:");
        foreach (var group in voteGroups.OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        }
    }

    /// <summary>
    /// Displays legend for abbreviated terms
    /// </summary>
    public void DisplayLegend()
    {
        Console.WriteLine("\n=== LEGEND ===");
        Console.WriteLine("MyVote:");
        Console.WriteLine("  Apprvd = Approved");
        Console.WriteLine("  ApSugg = Approved with Suggestions");
        Console.WriteLine("  NoVote = No Vote");
        Console.WriteLine("  Wait4A = Waiting for Author");
        Console.WriteLine("  Reject = Rejected");
        Console.WriteLine("  ---    = Not a reviewer");
        Console.WriteLine();
        Console.WriteLine("Reason:");
        Console.WriteLine("  PendRv = Pending Reviewer Approval (API reviewers)");
        Console.WriteLine("  PendOt = Pending Other Approvals");
        Console.WriteLine("  Policy = Policy/Build issues");
        Console.WriteLine("  Wait4A = Waiting for Author");
        Console.WriteLine("  Reject = Rejected");
        Console.WriteLine();
        Console.WriteLine("Change:");
        Console.WriteLine("  Actor: Action (e.g., 'Me: Approved', 'Author: Pushed Code')");
        Console.WriteLine("  Actor = Me|Author|Reviewer|Other");
        Console.WriteLine("  Action = Created PR|Pushed Code|Added Comment|Resolved Comment|Approved|Waiting for Author|Rejected");
    }

    private static string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;
            
        if (input.Length <= maxLength)
            return input;
            
        return input.Substring(0, maxLength - 3) + "...";
    }

    private static string FormatStatus(Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus status)
    {
        return status switch
        {
            Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus.Active => "Active",
            Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus.Completed => "Complete",
            Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus.Abandoned => "Abandon",
            _ => "Unknown"
        };
    }
}
