namespace gapir.Models;

public class GapirResult(IReadOnlyList<PullRequestInfo> approvedPRs, IReadOnlyList<PullRequestInfo> pendingPRs, string? errorMessage)
{
    [Obsolete("title should be set in tool, not the library")]
    public string? Title { get; set; }

    public IReadOnlyList<PullRequestInfo> PendingPRs { get; set; } = pendingPRs;

    public IReadOnlyList<PullRequestInfo> ApprovedPRs { get; set; } = approvedPRs;

    public string? ErrorMessage { get; set; } = errorMessage;
}
