using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service for handling completed pull request operations
/// </summary>
public class CompletedPullRequestService
{
    private readonly CompletedPullRequestDataLoader _dataLoader;
    private readonly PullRequestAnalyzer _analyzer;

    public CompletedPullRequestService(CompletedPullRequestDataLoader dataLoader, PullRequestAnalyzer analyzer)
    {
        _dataLoader = dataLoader;
        _analyzer = analyzer;
    }

    /// <summary>
    /// Gets all pull requests completed in the last N days where the current user was a reviewer
    /// </summary>
    public async Task<CompletedPullRequestResult> GetCompletedPullRequestsAsync(VssConnection connection, int daysBack = 30)
    {
        var analysisResult = await _dataLoader.LoadCompletedPullRequestsAsync(connection, daysBack);
        var completedPRs = _analyzer.FilterCompletedPullRequests(analysisResult.AllPullRequests);
        var statistics = _analyzer.GetStatistics(analysisResult.AllPullRequests);

        return new CompletedPullRequestResult
        {
            CompletedPullRequests = completedPRs,
            Statistics = statistics,
            CurrentUserDisplayName = analysisResult.CurrentUserDisplayName,
            DaysBack = daysBack
        };
    }
}

/// <summary>
/// Result for completed pull request operations
/// </summary>
public class CompletedPullRequestResult
{
    public IEnumerable<PullRequestInfo> CompletedPullRequests { get; set; } = Enumerable.Empty<PullRequestInfo>();
    public PullRequestStatistics Statistics { get; set; } = new();
    public string CurrentUserDisplayName { get; set; } = string.Empty;
    public int DaysBack { get; set; } = 30;
}