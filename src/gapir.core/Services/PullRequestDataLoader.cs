using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service that loads all pull request data from Azure DevOps.
/// This is the shared data access layer used by all PR-related commands.
/// </summary>
public class PullRequestDataLoader
{
    private readonly ApiReviewersGroupService _groupService;

    public PullRequestDataLoader(ApiReviewersGroupService groupService)
    {
        _groupService = groupService;
    }

    /// <summary>
    /// Loads all pull requests assigned to the current user from Azure DevOps
    /// </summary>
    public async Task<PullRequestAnalysisResult> LoadPullRequestsAsync(VssConnection connection)
    {
        try
        {
            // Get repository information
            var gitClient = connection.GetClient<GitHttpClient>();
            var repository = await gitClient.GetRepositoryAsync(AdoConfig.ProjectName, AdoConfig.RepositoryName);

            var (currentUserId, currentUserDisplayName) = GetCurrentUserInfo(connection);
            Log.Information($"Checking pull requests for user: {currentUserDisplayName}");

            // Pre-load API reviewers group members
            Log.Information("Loading API reviewers group...");
            var apiReviewersMembers = await _groupService.GetGroupMembersAsync(connection);

            // Initialize analysis service
            var analysisService = new PullRequestAnalysisService(
                apiReviewersMembers,
                currentUserId,
                currentUserDisplayName);

            // Get pull requests assigned to the current user
            Log.Information("Fetching pull requests...");
            var searchCriteria = new GitPullRequestSearchCriteria()
            {
                Status = PullRequestStatus.Active,
                ReviewerId = currentUserId
            };

            var pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
            Log.Success($"Found {pullRequests.Count} assigned pull requests");

            // Analyze all pull requests
            var pullRequestInfos = await analysisService.AnalyzePullRequestsAsync(
                pullRequests,
                gitClient,
                repository.Id);

            return new PullRequestAnalysisResult
            {
                AllPullRequests = pullRequestInfos,
                CurrentUserId = currentUserId,
                CurrentUserDisplayName = currentUserDisplayName
            };
        }
        catch (Exception ex)
        {
            Log.Error($"Error loading pull request data: {ex.Message}");
            throw;
        }
    }

    private static (Guid currentUserId, string currentUserDisplayName) GetCurrentUserInfo(VssConnection connection)
    {
        var currentUserId = connection.AuthorizedIdentity.Id;
        var currentUserDisplayName = connection.AuthorizedIdentity.DisplayName ?? "Unknown User";
        return (currentUserId, currentUserDisplayName);
    }
}

/// <summary>
/// Result container for pull request analysis
/// </summary>
public class PullRequestAnalysisResult
{
    public IEnumerable<PullRequestInfo> AllPullRequests { get; set; } = Enumerable.Empty<PullRequestInfo>();
    public Guid CurrentUserId { get; set; }
    public string CurrentUserDisplayName { get; set; } = string.Empty;
}
