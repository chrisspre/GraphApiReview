namespace gapir.Services;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;

/// <summary>
/// Service responsible for diagnosing individual pull requests and providing detailed reviewer information
/// </summary>
public class PullRequestDiagnosticService
{
    public async Task<PullRequestDiagnosticResult> DiagnosePullRequestAsync(VssConnection connection, int prId)
    {
        var result = new PullRequestDiagnosticResult { PullRequestId = prId };

        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();
            
            // Get repository information the same way as the main tool
            var repository = await gitClient.GetRepositoryAsync(AdoConfig.ProjectName, AdoConfig.RepositoryName);
            
            // Get the specific PR
            var pr = await gitClient.GetPullRequestAsync(repository.Id, prId);
            
            result.Title = pr.Title;
            result.Status = pr.Status.ToString();
            result.CreatedBy = pr.CreatedBy?.DisplayName ?? "Unknown";
            result.CreationDate = pr.CreationDate;
            result.ReviewersCount = pr.Reviewers?.Count() ?? 0;
            result.Reviewers = pr.Reviewers?.ToList() ?? new List<IdentityRefWithVote>();
            
            // Get current user info
            var currentUser = connection.AuthorizedIdentity;
            result.CurrentUserId = currentUser.Id;
            result.CurrentUserDisplayName = currentUser.DisplayName;
            
            // Find current user in reviewers list
            result.CurrentUserReviewer = pr.Reviewers?.FirstOrDefault(r => 
                r.Id.Equals(currentUser.Id) || 
                r.DisplayName.Equals(currentUser.DisplayName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
