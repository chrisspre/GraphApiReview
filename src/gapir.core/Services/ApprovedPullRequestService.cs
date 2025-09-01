using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service for handling approved pull request operations
/// </summary>
public class ApprovedPullRequestService
{
    private readonly PullRequestDataLoader _dataLoader;
    private readonly PullRequestAnalyzer _analyzer;

    public ApprovedPullRequestService(PullRequestDataLoader dataLoader, PullRequestAnalyzer analyzer)
    {
        _dataLoader = dataLoader;
        _analyzer = analyzer;
    }

    /// <summary>
    /// Gets all pull requests already approved by the current user
    /// </summary>
    public async Task<ApprovedPullRequestResult> GetApprovedPullRequestsAsync(VssConnection connection)
    {
        var analysisResult = await _dataLoader.LoadPullRequestsAsync(connection);
        var approvedPRs = _analyzer.FilterApprovedPullRequests(analysisResult.AllPullRequests);
        var statistics = _analyzer.GetStatistics(analysisResult.AllPullRequests);

        return new ApprovedPullRequestResult
        {
            ApprovedPullRequests = approvedPRs,
            Statistics = statistics,
            CurrentUserDisplayName = analysisResult.CurrentUserDisplayName
        };
    }
}

/// <summary>
/// Result for approved pull request operations
/// </summary>
public class ApprovedPullRequestResult
{
    public IEnumerable<PullRequestInfo> ApprovedPullRequests { get; set; } = Enumerable.Empty<PullRequestInfo>();
    public PullRequestStatistics Statistics { get; set; } = new();
    public string CurrentUserDisplayName { get; set; } = string.Empty;
}
