namespace gapir.Models;

/// <summary>
/// Represents timing information for a reviewer on a pull request
/// </summary>
public class ReviewerTimingInfo
{
    /// <summary>
    /// The reviewer's identity information
    /// </summary>
    public ReviewerIdentity Reviewer { get; set; } = new();
    
    /// <summary>
    /// When the reviewer was first assigned to the PR
    /// </summary>
    public DateTime? AssignedAt { get; set; }
    
    /// <summary>
    /// When the reviewer approved the PR (voted 10)
    /// </summary>
    public DateTime? ApprovedAt { get; set; }
    
    /// <summary>
    /// Duration from approval to PR completion (if this reviewer approved and PR is completed)
    /// </summary>
    public TimeSpan? TimeFromApprovalToCompletion { get; set; }
    
    /// <summary>
    /// The final vote value for this reviewer
    /// </summary>
    public int? FinalVote { get; set; }
    
    /// <summary>
    /// Whether this reviewer was required
    /// </summary>
    public bool IsRequired { get; set; }
}

/// <summary>
/// Represents reviewer identity information
/// </summary>
public class ReviewerIdentity
{
    /// <summary>
    /// Reviewer display name
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// Reviewer unique identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Reviewer unique name (email)
    /// </summary>
    public string? UniqueName { get; set; }
}

/// <summary>
/// Represents comprehensive timing information for a pull request
/// </summary>
public class PullRequestTimingInfo
{
    /// <summary>
    /// Pull request ID
    /// </summary>
    public int PullRequestId { get; set; }
    
    /// <summary>
    /// When the PR was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the PR was completed/merged
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Total time from creation to completion
    /// </summary>
    public TimeSpan? TotalDuration => 
        CompletedAt.HasValue ? CompletedAt.Value - CreatedAt : null;
    
    /// <summary>
    /// Timing information for each reviewer
    /// </summary>
    public List<ReviewerTimingInfo> ReviewerTimings { get; set; } = new();
    
    /// <summary>
    /// Time when the first reviewer was assigned
    /// </summary>
    public DateTime? FirstReviewerAssignedAt => 
        ReviewerTimings
            .Where(r => r.AssignedAt.HasValue)
            .OrderBy(r => r.AssignedAt)
            .FirstOrDefault()?.AssignedAt;
    
    /// <summary>
    /// Time when the first approval was received
    /// </summary>
    public DateTime? FirstApprovalAt => 
        ReviewerTimings
            .Where(r => r.ApprovedAt.HasValue)
            .OrderBy(r => r.ApprovedAt)
            .FirstOrDefault()?.ApprovedAt;
    
    /// <summary>
    /// Time when all required reviewers approved
    /// </summary>
    public DateTime? AllRequiredApprovedAt => 
        ReviewerTimings
            .Where(r => r.IsRequired && r.ApprovedAt.HasValue)
            .OrderByDescending(r => r.ApprovedAt)
            .FirstOrDefault()?.ApprovedAt;
    
    /// <summary>
    /// Duration from first assignment to first approval
    /// </summary>
    public TimeSpan? TimeToFirstApproval => 
        FirstReviewerAssignedAt.HasValue && FirstApprovalAt.HasValue
            ? FirstApprovalAt.Value - FirstReviewerAssignedAt.Value
            : null;
    
    /// <summary>
    /// Duration from all required approvals to completion
    /// </summary>
    public TimeSpan? TimeFromApprovalToCompletion =>
        AllRequiredApprovedAt.HasValue && CompletedAt.HasValue
            ? CompletedAt.Value - AllRequiredApprovedAt.Value
            : null;
}

/// <summary>
/// Represents timing data extracted from PR threads for a specific reviewer
/// </summary>
public class ReviewerTimingData
{
    /// <summary>
    /// Reviewer's display name
    /// </summary>
    public string ReviewerDisplayName { get; set; } = string.Empty;
    
    /// <summary>
    /// When the reviewer was assigned to the PR
    /// </summary>
    public DateTime? AssignedDateTime { get; set; }
    
    /// <summary>
    /// When the reviewer voted on the PR
    /// </summary>
    public DateTime? VoteDateTime { get; set; }
    
    /// <summary>
    /// When the PR was completed
    /// </summary>
    public DateTime? CompletedDateTime { get; set; }
    
    /// <summary>
    /// The reviewer's vote text (e.g., "Approved", "Waiting for author")
    /// </summary>
    public string? Vote { get; set; }
    
    /// <summary>
    /// Duration from assignment to approval (A2A)
    /// </summary>
    public TimeSpan? AssignmentToApprovalDuration =>
        AssignedDateTime.HasValue && VoteDateTime.HasValue
            ? VoteDateTime.Value - AssignedDateTime.Value
            : null;
    
    /// <summary>
    /// Duration from approval to completion (A2C)
    /// </summary>
    public TimeSpan? ApprovalToCompletionDuration =>
        VoteDateTime.HasValue && CompletedDateTime.HasValue
            ? CompletedDateTime.Value - VoteDateTime.Value
            : null;
}

/// <summary>
/// Represents a completed pull request with timing data
/// </summary>
public class CompletedPullRequestWithTiming
{
    /// <summary>
    /// The pull request information
    /// </summary>
    public Microsoft.TeamFoundation.SourceControl.WebApi.GitPullRequest PullRequest { get; set; } = new();
    
    /// <summary>
    /// The author of the pull request
    /// </summary>
    public string Author { get; set; } = string.Empty;
    
    /// <summary>
    /// The reviewer's vote on the pull request
    /// </summary>
    public string Vote { get; set; } = string.Empty;
    
    /// <summary>
    /// PR threads containing comments and timing data
    /// </summary>
    public List<Microsoft.TeamFoundation.SourceControl.WebApi.GitPullRequestCommentThread> Threads { get; set; } = new();
    
    /// <summary>
    /// Extracted timing data for the reviewer
    /// </summary>
    public ReviewerTimingData TimingData { get; set; } = new();
}

/// <summary>
/// Result containing completed pull requests with timing information
/// </summary>
public class CompletedPullRequestWithTimingResult
{
    /// <summary>
    /// List of completed pull requests with timing data
    /// </summary>
    public List<CompletedPullRequestWithTiming> CompletedPullRequests { get; set; } = new();
    
    /// <summary>
    /// Total count of pull requests
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}