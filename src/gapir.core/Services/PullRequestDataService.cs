namespace gapir.Services;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;

public class PullRequestDataService
{
    // Store API reviewers group members to avoid repeated API calls
    private HashSet<string>? _apiReviewersMembers;
    private readonly ApiReviewersGroupService _groupService;
    private readonly AzureDevOpsConfiguration _adoConfig;

    public PullRequestDataService(ApiReviewersGroupService groupService, AzureDevOpsConfiguration adoConfig)
    {
        _groupService = groupService;
        _adoConfig = adoConfig;
    }

    public async Task<GapirResult> GetPullRequestDataAsync(VssConnection connection)    
    {
        // var result = new GapirResult
        // {
        //     Title = "gapir (Graph API Review) - Azure DevOps Pull Request Checker"
        // };

        try
        {
            // Get repository information first
            var gitClient = connection.GetClient<GitHttpClient>();
            var repository = await gitClient.GetRepositoryAsync(_adoConfig.ProjectName, _adoConfig.RepositoryName);

            var (currentUserId, currentUserDisplayName) = GetCurrentUserInfo(connection);
            Log.Information($"Checking pull requests for user: {currentUserDisplayName}");

            // Pre-load API reviewers group members
            Log.Information("Loading API reviewers group...");
            _apiReviewersMembers = await _groupService.GetGroupMembersAsync(connection);


            // Initialize analysis service
            var analysisService = new PullRequestAnalysisService(
                _apiReviewersMembers,
                currentUserId,
                currentUserDisplayName);

            // Get pull requests assigned to the current user
            Log.Information("Fetching pull requests...");
            var searchCriteria = new GitPullRequestSearchCriteria()
            {
                Status = PullRequestStatus.Active,
                ReviewerId = currentUserId
            };

            List<GitPullRequest> pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
            Log.Success($"Found {pullRequests.Count} assigned pull requests");

            // Analyze all pull requests
            var pullRequestInfos = await analysisService.AnalyzePullRequestsAsync(
                pullRequests,
                gitClient,
                repository.Id);

            // populate pending PRs (core functionality)
            var pendingPRs = pullRequestInfos.Where(info => !info.IsApprovedByMe && info.MyVoteStatus != "---").ToList();

            var approvedPRs = pullRequestInfos.Where(info => info.IsApprovedByMe).ToList();
            // var notRequiredPRs = pullRequestInfos.Where(info => info.MyVoteStatus == "---").ToList();
            // var ApprovedPRs = approvedPRs.Concat(notRequiredPRs).ToArray();

            return new GapirResult(approvedPRs, pendingPRs, null);
        }
        catch (Exception ex)
        {
            Log.Error($"Error occurred while checking pull requests: {ex.Message}");
            return new GapirResult([], [], ex.Message);                        
        }
    }

    private (Guid UserId, string DisplayName) GetCurrentUserInfo(VssConnection connection)
    {
        try
        {
            var currentUser = connection.AuthorizedIdentity;
            return (currentUser.Id, currentUser.DisplayName);
        }
        catch
        {
            return (Guid.Empty, "Unknown User");
        }
    }
}
