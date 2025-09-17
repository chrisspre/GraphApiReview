namespace gapir.Models;

public class GapirResult(IReadOnlyList<PullRequestInfo> approvedPRs, IReadOnlyList<PullRequestInfo> pendingPRs, string? errorMessage)
{
    public IReadOnlyList<PullRequestInfo> PendingPRs { get; set; } = pendingPRs;

    public IReadOnlyList<PullRequestInfo> ApprovedPRs { get; set; } = approvedPRs;

    public string? ErrorMessage { get; set; } = errorMessage;
}
