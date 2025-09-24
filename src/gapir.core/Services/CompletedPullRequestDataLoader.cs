using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Location.Client;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service responsible for loading completed pull request data from Azure DevOps
/// </summary>
public class CompletedPullRequestDataLoader
{
    private readonly ApiReviewersGroupService _apiReviewersGroupService;
    private readonly AzureDevOpsConfiguration _adoConfig;

    public CompletedPullRequestDataLoader(ApiReviewersGroupService apiReviewersGroupService, AzureDevOpsConfiguration adoConfig)
    {
        _apiReviewersGroupService = apiReviewersGroupService;
        _adoConfig = adoConfig;
    }

    /// <summary>
    /// Loads completed pull requests from the last N days where the current user was a reviewer
    /// </summary>
    public async Task<PullRequestAnalysisResult> LoadCompletedPullRequestsAsync(VssConnection connection, int daysBack = 30)
    {
        try
        {
            Log.Information("Getting current user information...");
            var (currentUserId, currentUserDisplayName) = GetCurrentUserInfo(connection);
            Log.Success($"Current user: {currentUserDisplayName} ({currentUserId})");

            // Get API reviewers list
            var apiReviewersMembers = await _apiReviewersGroupService.GetGroupMembersAsync(connection);

            // Get the repository
            var gitClient = connection.GetClient<GitHttpClient>();
            var repository = await gitClient.GetRepositoryAsync(_adoConfig.ProjectName, _adoConfig.RepositoryName);

            // Initialize analysis service
            var analysisService = new PullRequestAnalysisService(
                apiReviewersMembers,
                currentUserId,
                currentUserDisplayName);

            // Get completed pull requests from the last N days where current user was a reviewer
            Log.Information($"Fetching completed pull requests from last {daysBack} days...");
            var cutoffDate = DateTime.UtcNow.AddDays(-daysBack);
            
            var searchCriteria = new GitPullRequestSearchCriteria()
            {
                Status = PullRequestStatus.Completed,
                ReviewerId = currentUserId
            };

            List<GitPullRequest> pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
            
            // Filter to only PRs completed in the last N days
            var recentCompletedPRs = pullRequests
                .Where(pr => pr.ClosedDate >= cutoffDate)
                .ToList();
            
            Log.Success($"Found {recentCompletedPRs.Count} completed pull requests from last {daysBack} days");

            // Analyze all pull requests
            var pullRequestInfos = await analysisService.AnalyzePullRequestsAsync(
                recentCompletedPRs,
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
            Log.Error($"Error occurred while loading completed pull requests: {ex.Message}");
            throw;
        }
    }

    private static (Guid UserId, string DisplayName) GetCurrentUserInfo(VssConnection connection)
    {
        var currentUserId = connection.AuthorizedIdentity.Id;
        var currentUserDisplayName = connection.AuthorizedIdentity.DisplayName ?? "Unknown User";
        return (currentUserId, currentUserDisplayName);
    }
}