namespace gapir.Models;

using Microsoft.TeamFoundation.SourceControl.WebApi;

/// <summary>
/// Represents enriched pull request information with calculated status and metadata
/// </summary>
public class PullRequestInfo
{
    public GitPullRequest PullRequest { get; set; } = null!;
    public string MyVoteStatus { get; set; } = string.Empty;
    public string ApiApprovalRatio { get; set; } = string.Empty;
    public string TimeAssigned { get; set; } = string.Empty;
    public string LastChangeInfo { get; set; } = string.Empty;
    public string PendingReason { get; set; } = string.Empty;
    public bool IsApprovedByMe { get; set; }
    public string ShortUrl { get; set; } = string.Empty;
    public string FullUrl { get; set; } = string.Empty;

    // Calculated properties for easy access
    public string Title => PullRequest?.Title ?? string.Empty;
    public string AuthorName => PullRequest?.CreatedBy?.DisplayName ?? string.Empty;
    public DateTime CreationDate => PullRequest?.CreationDate ?? DateTime.MinValue;
    public int PullRequestId => PullRequest?.PullRequestId ?? 0;
}
