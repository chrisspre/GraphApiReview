namespace gapir.Services;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using gapir.Models;

/// <summary>
/// Service responsible for analyzing pull requests and calculating their status
/// </summary>
public class PullRequestAnalysisService
{
    private readonly HashSet<string> _apiReviewersMembers;
    private readonly Guid _currentUserId;
    private readonly string _currentUserDisplayName;
    // private readonly bool _useShortUrls;

    public PullRequestAnalysisService(
        HashSet<string> apiReviewersGroupMembers, 
        Guid currentUserId, 
        string currentUserDisplayName)
    {
        _apiReviewersMembers = apiReviewersGroupMembers;
        _currentUserId = currentUserId;
        _currentUserDisplayName = currentUserDisplayName;
        // _useShortUrls = useShortUrls;
    }

    /// <summary>
    /// Analyzes a pull request and returns enriched information
    /// </summary>
    public async Task<PullRequestInfo> AnalyzePullRequestAsync(GitPullRequest pr, GitHttpClient gitClient, Guid repositoryId)
    {
        var info = new PullRequestInfo
        {
            PullRequest = pr,
            IsApprovedByMe = IsApprovedByCurrentUser(pr),
            MyVoteStatus = GetMyVoteStatus(pr),
            ApiApprovalRatio = GetApiApprovalRatio(pr),
            TimeAssigned = GetTimeAssignedToReviewer(pr),
            PendingReason = GetPendingReason(pr),
            // ShortUrl = GetShortUrl(pr),
            // FullUrl = GetFullUrl(pr)
        };

        // Get last change info (requires async call)
        info.LastChangeInfo = await GetLastChangeInfoAsync(gitClient, repositoryId, pr);

        return info;
    }

    /// <summary>
    /// Analyzes multiple pull requests concurrently
    /// </summary>
    public async Task<List<PullRequestInfo>> AnalyzePullRequestsAsync(
        List<GitPullRequest> pullRequests, 
        GitHttpClient gitClient, 
        Guid repositoryId)
    {
        var tasks = pullRequests.Select(pr => AnalyzePullRequestAsync(pr, gitClient, repositoryId));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private bool IsApprovedByCurrentUser(GitPullRequest pr)
    {
        var currentUserReviewer = pr.Reviewers?.FirstOrDefault(r =>
            r.Id.Equals(_currentUserId) ||
            r.DisplayName.Equals(_currentUserDisplayName, StringComparison.OrdinalIgnoreCase));

        return currentUserReviewer?.Vote == 10 && currentUserReviewer.IsRequired == true;
    }

    private string GetMyVoteStatus(GitPullRequest pr)
    {
        try
        {
            var currentUserReviewer = pr.Reviewers?.FirstOrDefault(r =>
                r.Id.Equals(_currentUserId) ||
                r.DisplayName.Equals(_currentUserDisplayName, StringComparison.OrdinalIgnoreCase));

            if (currentUserReviewer == null || currentUserReviewer.IsRequired != true)
                return "---"; // Not a reviewer or not required

            return currentUserReviewer.Vote switch
            {
                10 => "Apprvd", // Approved
                5 => "Sugges", // Approved with suggestions
                0 => "NoVote", // No vote
                -5 => "Wait4A", // Waiting for author (you requested changes)
                -10 => "Reject", // Rejected
                _ => "Unknow" // Unknown
            };
        }
        catch
        {
            return "Error"; // Error
        }
    }

    private string GetApiApprovalRatio(GitPullRequest pr)
    {
        try
        {
            if (_apiReviewersMembers.Count == 0)
                return "?/?"; // No API reviewers data available

            // Filter to only API reviewers who are required - check both email addresses and unique names
            var apiReviewers = pr.Reviewers?.Where(r => 
                (r.IsRequired == true) && // Only count required reviewers
                (_apiReviewersMembers.Contains(r.UniqueName) || 
                _apiReviewersMembers.Contains(r.Id.ToString()))).ToList();

            if (apiReviewers == null || apiReviewers.Count == 0)
            {
                return "0/0"; // No API reviewers assigned
            }
            var approvedCount = apiReviewers.Count(r => r.Vote >= 5); // 5 = approved with suggestions, 10 = approved
            var totalCount = apiReviewers.Count;

            return $"{approvedCount}/{totalCount}";
        }
        catch
        {
            return "?/?";
        }
    }

    private string GetTimeAssignedToReviewer(GitPullRequest pr)
    {
        try
        {
            // For now, we'll use the PR creation date as the baseline for reviewer assignment
            // Azure DevOps API doesn't directly provide reviewer assignment timestamps in the basic PR data
            // To get exact assignment times, we would need to query the pull request timeline/updates API
            var timeSinceCreation = DateTime.UtcNow - pr.CreationDate;
            var formattedTime = FormatTimeDifference(timeSinceCreation);
            
            // Show time since PR creation in concise format to fit in the Age column (max width 10 chars)
            return $"{formattedTime} ago";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetPendingReason(GitPullRequest pr)
    {
        try
        {
            // Check if there are any reviewers and get all reviewers
            var allReviewers = pr.Reviewers?.ToList() ?? new List<IdentityRefWithVote>();

            // Check for rejections first (highest priority) - from any reviewer
            var rejectedCount = allReviewers.Count(r => r.Vote == -10);
            if (rejectedCount > 0)
                return "Reject"; // Rejected

            // Check for waiting for author - from any reviewer
            var waitingForAuthorCount = allReviewers.Count(r => r.Vote == -5);
            if (waitingForAuthorCount > 0)
                return "Wait4A"; // Waiting For Author

            // Identify API reviewers using group membership data
            var apiReviewers = allReviewers.Where(r => _apiReviewersMembers.Contains(r.Id.ToString())).ToList();
            var nonApiReviewers = allReviewers.Where(r =>
                !_apiReviewersMembers.Contains(r.Id.ToString()) &&
                !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // Check API reviewers approval status (need at least 2 approved)
            var apiApprovedCount = apiReviewers.Count(r => r.Vote >= 5);
            var apiRequiredCount = 2; // Policy requires at least 2 API reviewers

            // Check non-API reviewers approval status  
            var nonApiApprovedCount = nonApiReviewers.Count(r => r.Vote >= 5);
            var nonApiTotalCount = nonApiReviewers.Count;

            // If we don't have enough API approvals yet
            if (apiReviewers.Count > 0 && apiApprovedCount < apiRequiredCount)
                return "PendRv"; // Pending Reviewer Approval (API reviewers)

            // If API approvals are satisfied but non-API reviewers haven't all approved
            if (apiApprovedCount >= apiRequiredCount && nonApiTotalCount > 0 && nonApiApprovedCount < nonApiTotalCount)
                return "PendOt"; // Pending Other Approvals

            // If we get here, approvals are likely satisfied but there might be policy/build issues
            return "Policy"; // Policy/Build issues (could be build failure, branch policy, etc.)
        }
        catch
        {
            return "UNK"; // Unknown
        }
    }

    // private string GetShortUrl(GitPullRequest pr)
    // {
    //     if (_useShortUrls)
    //     {
    //         // Use Base62 encoding for shorter URLs
    //         string base62Id = Base62.Encode(pr.PullRequestId);
    //         return $"http://g/pr/{base62Id}";
    //     }
    //     else
    //     {
    //         return GetFullUrl(pr);
    //     }
    // }

    private async Task<string> GetLastChangeInfoAsync(GitHttpClient gitClient, Guid repositoryId, GitPullRequest pr)
    {
        try
        {
            DateTime? mostRecentActivityDate = null;
            string? mostRecentActivityAuthorId = null;
            string changeType = "Created";

            // Check 1: PR threads/comments - look for different types of comments
            var threads = await gitClient.GetThreadsAsync(repositoryId, pr.PullRequestId);
            if (threads?.Any() == true)
            {
                var mostRecentComment = threads
                    .Where(t => t.Comments?.Any() == true)
                    .SelectMany(t => t.Comments)
                    .OrderByDescending(c => c.LastUpdatedDate)
                    .FirstOrDefault();

                if (mostRecentComment?.LastUpdatedDate != null)
                {
                    mostRecentActivityDate = mostRecentComment.LastUpdatedDate;
                    mostRecentActivityAuthorId = mostRecentComment.Author?.Id;
                    
                    // Determine comment type based on thread properties
                    var thread = threads.FirstOrDefault(t => t.Comments?.Contains(mostRecentComment) == true);
                    if (thread?.Properties?.Any(p => p.Key == "Microsoft.TeamFoundation.Discussion.Status") == true)
                    {
                        var status = thread.Properties.FirstOrDefault(p => p.Key == "Microsoft.TeamFoundation.Discussion.Status").Value?.ToString();
                        changeType = status?.ToLower() switch
                        {
                            "active" => "Added Comment",
                            "fixed" => "Resolved Comment", 
                            "wontfix" => "Won't Fix Comment",
                            "closed" => "Closed Comment",
                            _ => "Added Comment"
                        };
                    }
                    else
                    {
                        changeType = "Added Comment";
                    }
                }
            }

            // Check 2: PR iterations (code pushes)
            try
            {
                var iterations = await gitClient.GetPullRequestIterationsAsync(repositoryId, pr.PullRequestId);
                var mostRecentIteration = iterations?.OrderByDescending(i => i.CreatedDate).FirstOrDefault();
                
                if (mostRecentIteration?.CreatedDate != null && 
                    (mostRecentActivityDate == null || mostRecentIteration.CreatedDate > mostRecentActivityDate))
                {
                    mostRecentActivityDate = mostRecentIteration.CreatedDate;
                    mostRecentActivityAuthorId = pr.CreatedBy.Id;
                    changeType = "Pushed Code";
                }
            }
            catch
            {
                // Ignore iteration errors - some permissions issues might occur
            }

            // Check 3: Reviewer votes/approvals (this will override if we detect a vote from the same person)
            if (pr.Reviewers?.Any() == true)
            {
                foreach (var reviewer in pr.Reviewers)
                {
                    if (reviewer.Vote != 0 && reviewer.Id.ToString() == mostRecentActivityAuthorId)
                    {
                        // If the most recent activity author also has a vote, show the vote instead
                        changeType = reviewer.Vote switch
                        {
                            -10 => "Rejected",
                            -5 => "Waiting for Author",
                            5 => "Approved with Suggestions",
                            10 => "Approved",
                            _ => "Voted"
                        };
                        break;
                    }
                }
            }

            // Fallback to PR creation if no activity found
            if (string.IsNullOrEmpty(mostRecentActivityAuthorId))
            {
                mostRecentActivityAuthorId = pr.CreatedBy.Id;
                changeType = "Created PR";
            }

            // Determine the relationship (who)
            string actor;
            if (mostRecentActivityAuthorId == _currentUserId.ToString())
                actor = "Me";
            else if (mostRecentActivityAuthorId == pr.CreatedBy.Id)
                actor = "Author";
            else
            {
                var isReviewer = pr.Reviewers?.Any(r => r.Id.ToString() == mostRecentActivityAuthorId) == true;
                actor = isReviewer ? "Reviewer" : "Other";
            }

            // Format as "Who: What"
            return $"{actor}: {changeType}";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string FormatTimeDifference(TimeSpan timeDiff)
    {
        if (timeDiff.TotalDays >= 1)
            return $"{(int)timeDiff.TotalDays}d";
        else if (timeDiff.TotalHours >= 1)
            return $"{(int)timeDiff.TotalHours}h";
        else if (timeDiff.TotalMinutes >= 1)
            return $"{(int)timeDiff.TotalMinutes}m";
        else
            return "< 1m";
    }
}
