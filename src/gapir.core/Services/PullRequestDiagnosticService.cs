namespace gapir.Services;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Service responsible for diagnosing individual pull requests and providing detailed reviewer information.
/// Dumps raw Azure DevOps data as JSON for inspection.
/// </summary>
public class PullRequestDiagnosticService
{
    private readonly AzureDevOpsConfiguration _adoConfig;

    public PullRequestDiagnosticService(AzureDevOpsConfiguration adoConfig)
    {
        _adoConfig = adoConfig;
    }

    /// <summary>
    /// Diagnoses a PR by fetching raw data from Azure DevOps and saving it as a JSON file.
    /// Returns the file path where the diagnostic was saved.
    /// </summary>
    public async Task<string> DiagnosePullRequestAsync(VssConnection connection, int prId)
    {
        var gitClient = connection.GetClient<GitHttpClient>();
        var repository = await gitClient.GetRepositoryAsync(_adoConfig.ProjectName, _adoConfig.RepositoryName);
        var pr = await gitClient.GetPullRequestAsync(repository.Id, prId);

        // Fetch threads and iterations in parallel
        var threadsTask = gitClient.GetThreadsAsync(repository.Id, prId);
        var iterationsTask = gitClient.GetPullRequestIterationsAsync(repository.Id, prId);
        await Task.WhenAll(threadsTask, iterationsTask);

        var threads = await threadsTask;
        var iterations = await iterationsTask;

        var currentUser = connection.AuthorizedIdentity;

        // Build a raw JSON document with all relevant data
        var diagnostic = new JObject
        {
            ["pullRequestId"] = prId,
            ["title"] = pr.Title,
            ["status"] = pr.Status.ToString(),
            ["createdBy"] = pr.CreatedBy?.DisplayName,
            ["creationDate"] = pr.CreationDate,
            ["closedDate"] = pr.ClosedDate,
            ["sourceRefName"] = pr.SourceRefName,
            ["targetRefName"] = pr.TargetRefName,
            ["currentUser"] = new JObject
            {
                ["id"] = currentUser.Id.ToString(),
                ["displayName"] = currentUser.DisplayName
            },
            ["reviewers"] = new JArray(
                (pr.Reviewers ?? Enumerable.Empty<IdentityRefWithVote>()).Select(r => new JObject
                {
                    ["id"] = r.Id.ToString(),
                    ["displayName"] = r.DisplayName,
                    ["uniqueName"] = r.UniqueName,
                    ["vote"] = r.Vote,
                    ["isRequired"] = r.IsRequired,
                    ["isContainer"] = r.IsContainer
                })
            ),
            ["iterations"] = new JArray(
                (iterations ?? Enumerable.Empty<GitPullRequestIteration>()).Select(i => new JObject
                {
                    ["id"] = i.Id,
                    ["createdDate"] = i.CreatedDate,
                    ["description"] = i.Description
                })
            ),
            ["threads"] = new JArray(
                (threads ?? Enumerable.Empty<GitPullRequestCommentThread>()).Select(t => new JObject
                {
                    ["id"] = t.Id,
                    ["publishedDate"] = t.PublishedDate,
                    ["status"] = t.Status.ToString(),
                    ["properties"] = t.Properties != null
                        ? JObject.FromObject(t.Properties.ToDictionary(p => p.Key, p => p.Value?.ToString()))
                        : null,
                    ["comments"] = new JArray(
                        (t.Comments ?? Enumerable.Empty<Comment>()).Select(c => new JObject
                        {
                            ["id"] = c.Id,
                            ["commentType"] = c.CommentType.ToString(),
                            ["authorDisplayName"] = c.Author?.DisplayName,
                            ["authorId"] = c.Author?.Id,
                            ["publishedDate"] = c.PublishedDate,
                            ["lastUpdatedDate"] = c.LastUpdatedDate,
                            ["content"] = c.Content
                        })
                    )
                })
            )
        };

        // Write to file in current directory
        var fileName = $"pr-{prId}-diagnostic.json";
        var json = diagnostic.ToString(Formatting.Indented);
        await File.WriteAllTextAsync(fileName, json);

        return fileName;
    }
}
