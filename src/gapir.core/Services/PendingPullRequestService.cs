using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service for handling pending pull request operations
/// </summary>
public class PendingPullRequestService
{
    private readonly PullRequestDataLoader _dataLoader;
    private readonly PullRequestAnalyzer _analyzer;

    public PendingPullRequestService(PullRequestDataLoader dataLoader, PullRequestAnalyzer analyzer)
    {
        _dataLoader = dataLoader;
        _analyzer = analyzer;
    }

    /// <summary>
    /// Gets all pull requests pending review by the current user
    /// </summary>
    public async Task<PendingPullRequestResult> GetPendingPullRequestsAsync(VssConnection connection)
    {
        var analysisResult = await _dataLoader.LoadPullRequestsAsync(connection);
        var pendingPRs = _analyzer.FilterPendingPullRequests(analysisResult.AllPullRequests);
        var statistics = _analyzer.GetStatistics(analysisResult.AllPullRequests);

        return new PendingPullRequestResult
        {
            PendingPullRequests = pendingPRs,
            Statistics = statistics,
            CurrentUserDisplayName = analysisResult.CurrentUserDisplayName
        };
    }
}

/// <summary>
/// Result for pending pull request operations
/// </summary>
public class PendingPullRequestResult
{
    public IEnumerable<PullRequestInfo> PendingPullRequests { get; set; } = Enumerable.Empty<PullRequestInfo>();
    public PullRequestStatistics Statistics { get; set; } = new();
    public string CurrentUserDisplayName { get; set; } = string.Empty;
}
