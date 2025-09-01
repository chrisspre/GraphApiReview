namespace gapir.Models;

using Microsoft.TeamFoundation.SourceControl.WebApi;

/// <summary>
/// Result model for pull request diagnostic information
/// </summary>
public class PullRequestDiagnosticResult
{
    public int PullRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
    public int ReviewersCount { get; set; }
    public List<IdentityRefWithVote> Reviewers { get; set; } = new();
    public Guid CurrentUserId { get; set; }
    public string CurrentUserDisplayName { get; set; } = string.Empty;
    public IdentityRefWithVote? CurrentUserReviewer { get; set; }
    public string? ErrorMessage { get; set; }
}
