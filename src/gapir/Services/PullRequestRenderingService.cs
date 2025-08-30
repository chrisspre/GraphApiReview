namespace gapir.Services;

using gapir.Models;
using gapir.Utilities;
using System.Text.Json;

public class PullRequestRenderingService
{
    private readonly PullRequestCheckerOptions _options;

    public PullRequestRenderingService(PullRequestCheckerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public void RenderResult(GapirResult result)
    {
        if (_options.JsonOutput)
        {
            RenderJson(result);
        }
        else
        {
            RenderText(result);
        }
    }

    private void RenderJson(GapirResult result)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(result, options);
        Console.WriteLine(json);
    }

    private void RenderText(GapirResult result)
    {
        Console.WriteLine(result.Title);
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        if (result.ErrorMessage != null)
        {
            Log.Error($"Error: {result.ErrorMessage}");
            return;
        }

        // Show approved PRs if they are populated (regardless of the option)
        if (result.ApprovedPRs?.Any() == true)
        {
            Console.WriteLine($"✓ {result.ApprovedPRs.Count} PR(s) you have already approved or are not required to review:");
            RenderApprovedPRsTable(result.ApprovedPRs);
        }

        // Show pending PRs
        Console.WriteLine($"⏳ {result.PendingPRs.Count} incomplete PR(s) assigned to you:");
        if (result.PendingPRs.Any())
        {
            RenderPendingPRsTable(result.PendingPRs);
        }
        else
        {
            Console.WriteLine("No pull requests found requiring your review.");
        }
    }

    private void RenderApprovedPRsTable(List<PullRequestInfo> approvedPRs)
    {
        var displayService = new PullRequestDisplayService(_options.UseShortUrls, _options.ShowDetailedTiming);
        displayService.DisplayApprovedPullRequestsTable(approvedPRs);
    }

    private void RenderPendingPRsTable(List<PullRequestInfo> pendingPRs)
    {
        var displayService = new PullRequestDisplayService(_options.UseShortUrls, _options.ShowDetailedTiming);
        displayService.DisplayPullRequestsTable(pendingPRs);
    }
}
