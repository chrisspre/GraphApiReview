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