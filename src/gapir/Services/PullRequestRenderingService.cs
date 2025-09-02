namespace gapir.Services;

using gapir.Models;
using System.Text.Json;

public class PullRequestRenderingService
{
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
        var displayService = new PullRequestDisplayService(options.UseShortUrls, options.ShowDetailedTiming);
        displayService.DisplayApprovedPullRequestsTable(approvedPRs);
    }

    private void RenderPendingPRsTable(List<PullRequestInfo> pendingPRs, PullRequestRenderingOptions options)
    {
        var displayService = new PullRequestDisplayService(options.UseShortUrls, options.ShowDetailedTiming);
        displayService.DisplayPullRequestsTable(pendingPRs);
    }
}
