namespace gapir.Services;

public static class AdoConfig
{
    public const string ReviewersGroupName = "[TEAM FOUNDATION]\\Microsoft Graph API reviewers";

    public const string OrganizationUrl = "https://dev.azure.com/msazure";

    public const string ProjectName = "One";

    public const string RepositoryName = "AD-AggregatorService-Workloads";

    // Azure CLI Client ID for authentication
    public const string ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";


    public static string GetPullRequestUrl(int pullRequestId)
    {
        return $"https://msazure.visualstudio.com/{AdoConfig.ProjectName}/_git/{AdoConfig.RepositoryName}/pullrequest/{pullRequestId}";
    }

}
