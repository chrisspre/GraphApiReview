using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service that filters and analyzes pull request data.
/// Provides specific filtering logic for different command requirements.
/// </summary>
public class PullRequestAnalyzer
{
    /// <summary>
    /// Filters pull requests to show only those pending review from the current user
    /// </summary>
    public IEnumerable<PullRequestInfo> FilterPendingPullRequests(IEnumerable<PullRequestInfo> allPRs)
    {
        return allPRs.Where(info => 
            !info.IsApprovedByMe && 
            info.MyVoteStatus != "---" && 
            !info.IsCompleted);
    }

    /// <summary>
    /// Filters pull requests to show only those already approved by the current user
    /// </summary>
    public IEnumerable<PullRequestInfo> FilterApprovedPullRequests(IEnumerable<PullRequestInfo> allPRs)
    {
        return allPRs.Where(info => 
            info.IsApprovedByMe && 
            !info.IsCompleted);
    }

    /// <summary>
    /// Gets statistics about all pull requests
    /// </summary>
    public PullRequestStatistics GetStatistics(IEnumerable<PullRequestInfo> allPRs)
    {
        var prList = allPRs.ToList();
        
        return new PullRequestStatistics
        {
            TotalAssigned = prList.Count,
            PendingReview = FilterPendingPullRequests(prList).Count(),
            AlreadyApproved = FilterApprovedPullRequests(prList).Count()
        };
    }
}

/// <summary>
/// Statistics about pull request analysis
/// </summary>
public class PullRequestStatistics
{
    public int TotalAssigned { get; set; }
    public int PendingReview { get; set; }
    public int AlreadyApproved { get; set; }
}
