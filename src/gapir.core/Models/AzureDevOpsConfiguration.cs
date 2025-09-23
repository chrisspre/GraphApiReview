namespace gapir.Models;

/// <summary>
/// Configuration for Azure DevOps resources and endpoints
/// </summary>
public class AzureDevOpsConfiguration
{
    /// <summary>
    /// Name of the Azure DevOps reviewers group
    /// </summary>
    public string ReviewersGroupName { get; init; } = "[TEAM FOUNDATION]\\Microsoft Graph API reviewers";

    /// <summary>
    /// Azure DevOps organization URL
    /// </summary>
    public string OrganizationUrl { get; init; } = "https://dev.azure.com/msazure";

    /// <summary>
    /// Azure DevOps project name
    /// </summary>
    public string ProjectName { get; init; } = "One";

    /// <summary>
    /// Azure DevOps repository name
    /// </summary>
    public string RepositoryName { get; init; } = "AD-AggregatorService-Workloads";

    /// <summary>
    /// Generates a pull request URL for the given PR ID
    /// </summary>
    /// <param name="pullRequestId">The pull request ID</param>
    /// <returns>Full URL to the pull request</returns>
    public string GetPullRequestUrl(int pullRequestId)
    {
        return $"https://msazure.visualstudio.com/{ProjectName}/_git/{RepositoryName}/pullrequest/{pullRequestId}";
    }
}