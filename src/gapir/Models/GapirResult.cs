namespace gapir.Models;

public class GapirResult
{
    public string? Title { get; set; }
    public List<PullRequestInfo> PendingPRs { get; set; } = new();
    public List<PullRequestInfo>? ApprovedPRs { get; set; }
    public string? ErrorMessage { get; set; }
}
