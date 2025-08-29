namespace gapir.Services;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

/// <summary>
/// Service responsible for Azure DevOps integration and API calls
/// </summary>
public class AzureDevOpsService : IDisposable
{
    private readonly VssConnection _connection;
    private readonly GitHttpClient _gitClient;
    private readonly Guid _repositoryId;
    private readonly string _projectName;

    public AzureDevOpsService(string personalAccessToken, string organizationUrl, string projectName, Guid repositoryId)
    {
        _projectName = projectName;
        _repositoryId = repositoryId;
        
        var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
        _connection = new VssConnection(new Uri(organizationUrl), credentials);
        _gitClient = _connection.GetClient<GitHttpClient>();
    }

    /// <summary>
    /// Gets pull requests based on search criteria
    /// </summary>
    public async Task<List<GitPullRequest>> GetPullRequestsAsync(
        string? targetRefName = null,
        PullRequestStatus? status = null,
        string? creatorId = null,
        string? reviewerId = null,
        int? top = null)
    {
        var searchCriteria = new GitPullRequestSearchCriteria
        {
            TargetRefName = targetRefName,
            Status = status
        };

        // Convert string IDs to Guid if needed
        if (!string.IsNullOrEmpty(creatorId) && Guid.TryParse(creatorId, out var creatorGuid))
            searchCriteria.CreatorId = creatorGuid;
        
        if (!string.IsNullOrEmpty(reviewerId) && Guid.TryParse(reviewerId, out var reviewerGuid))
            searchCriteria.ReviewerId = reviewerGuid;

        var pullRequests = await _gitClient.GetPullRequestsAsync(
            _repositoryId,
            searchCriteria,
            top: top);

        return pullRequests;
    }

    /// <summary>
    /// Gets pull requests that are assigned to the "Microsoft Graph API reviewers" group
    /// </summary>
    public async Task<List<GitPullRequest>> GetApiReviewPullRequestsAsync(HashSet<string> apiReviewersMembers)
    {
        var allPullRequests = await GetPullRequestsAsync(
            targetRefName: "refs/heads/main",
            status: PullRequestStatus.Active,
            top: 200);

        // Filter to only PRs that have at least one required API reviewer assigned
        var apiReviewPrs = allPullRequests.Where(pr =>
            pr.Reviewers?.Any(r =>
                r.IsRequired == true && // Only required reviewers
                (apiReviewersMembers.Contains(r.UniqueName) ||
                apiReviewersMembers.Contains(r.Id.ToString()))) == true).ToList();

        return apiReviewPrs;
    }

    /// <summary>
    /// Gets pull request threads (comments)
    /// </summary>
    public async Task<List<GitPullRequestCommentThread>> GetPullRequestThreadsAsync(int pullRequestId)
    {
        return await _gitClient.GetThreadsAsync(_repositoryId, pullRequestId);
    }

    /// <summary>
    /// Gets pull request iterations (code pushes)
    /// </summary>
    public async Task<List<GitPullRequestIteration>> GetPullRequestIterationsAsync(int pullRequestId)
    {
        return await _gitClient.GetPullRequestIterationsAsync(_repositoryId, pullRequestId);
    }

    /// <summary>
    /// Gets the current user information
    /// </summary>
    public (Guid UserId, string DisplayName) GetCurrentUserInfo()
    {
        try
        {
            var currentUser = _connection.AuthorizedIdentity;
            return (currentUser.Id, currentUser.DisplayName);
        }
        catch
        {
            return (Guid.Empty, "Unknown User");
        }
    }

    /// <summary>
    /// Gets the Git client for direct access
    /// </summary>
    public GitHttpClient GetGitClient()
    {
        return _gitClient;
    }

    /// <summary>
    /// Gets the repository ID
    /// </summary>
    public Guid GetRepositoryId()
    {
        return _repositoryId;
    }

    /// <summary>
    /// Gets the project name
    /// </summary>
    public string GetProjectName()
    {
        return _projectName;
    }

    public void Dispose()
    {
        _gitClient?.Dispose();
        _connection?.Dispose();
    }
}
