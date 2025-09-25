using gapir.Services;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using gapir.Models;
using gapir.Extensions;

namespace gapir;

public class ReviewerCollector
{
    private readonly AzureDevOpsConfiguration _adoConfig;
    private readonly ConsoleAuth _consoleAuth;

    public ReviewerCollector(AzureDevOpsConfiguration adoConfig, ConsoleAuth consoleAuth)
    {
        _adoConfig = adoConfig;
        _consoleAuth = consoleAuth;
    }

    private const int MaxPrsToAnalyze = 500;

    public async Task CollectAndGenerateAsync()
    {
        // Get authenticated connection
        using var connection = await _consoleAuth.AuthenticateAsync();
        if (connection == null)
        {
            Console.WriteLine("[ERROR] Authentication failed.");
            return;
        }

        await CollectAndGenerateAsync(connection);
    }

    public async Task CollectAndGenerateAsync(VssConnection connection)
    {
        Console.WriteLine("Pull Request Required Reviewers Analyzer");
        Console.WriteLine(new string('=', 50));

        try
        {

            var gitClient = connection.GetClient<GitHttpClient>();
            var reviewerCounts = new Dictionary<string, (int count, string displayName)>();

            Console.WriteLine($"\n[INFO] Analyzing repository: {_adoConfig.RepositoryName}");
            Console.WriteLine($"[INFO] Fetching recent pull requests (limit: {MaxPrsToAnalyze})...");

            try
            {
                // Get recent completed PRs
                var prs = await gitClient.GetPullRequestsAsync(
                    project: _adoConfig.ProjectName,
                    repositoryId: _adoConfig.RepositoryName,
                    searchCriteria: new GitPullRequestSearchCriteria
                    {
                        Status = PullRequestStatus.Completed
                    },
                    top: MaxPrsToAnalyze);

                Console.WriteLine($"[OK] Found {prs.Count} completed pull requests in {_adoConfig.RepositoryName}");

                if (prs.Count == 0)
                {
                    Console.WriteLine($"[SKIP] No PRs found in {_adoConfig.RepositoryName}, skipping...");
                    return;
                }

                Console.WriteLine("[INFO] Analyzing required reviewers...");
                int apiReviewPrsFound = 0;

                foreach (var pr in prs)
                {
                    try
                    {
                        // Get detailed PR with reviewers
                        var detailedPr = await gitClient.GetPullRequestAsync(
                            project: _adoConfig.ProjectName,
                            repositoryId: _adoConfig.RepositoryName,
                            pullRequestId: pr.PullRequestId);

                        if (detailedPr?.Reviewers != null)
                        {
                            // First check if this PR has the "Microsoft Graph API reviewers" group assigned
                            bool hasApiReviewersGroup = detailedPr.Reviewers.Any(r =>
                                r.DisplayName?.Contains("Microsoft Graph API reviewers", StringComparison.OrdinalIgnoreCase) == true ||
                                r.UniqueName?.Contains("Microsoft Graph API reviewers", StringComparison.OrdinalIgnoreCase) == true);

                            if (!hasApiReviewersGroup)
                            {
                                // Skip this PR - it's not an API review PR
                                continue;
                            }

                            apiReviewPrsFound++;
                            var names = string.Join(", ", detailedPr.Reviewers.Where(r => GetReviewerKey(r) is { } reviewerKey).Select(r => r.DisplayName.SubstringBefore(' ')));
                            Console.WriteLine($"  API Review PR #{pr.PullRequestId}: {TruncateString(pr.Title, 50)}... {names}");

                            foreach (var reviewer in detailedPr.Reviewers)
                            {
                                if (reviewer.IsRequired == true)
                                {
                                    // Only consider individual reviewers (exclude groups and tools)
                                    if (GetReviewerKey(reviewer) is { } reviewerKey)
                                    {
                                        var displayName = reviewer.DisplayName ?? reviewerKey ?? "Unknown";
                                        
                                        if (reviewerCounts.TryGetValue(reviewerKey!, out var value))
                                        {
                                            reviewerCounts[reviewerKey!] = (value.count + 1, displayName);
                                        }
                                        else
                                        {
                                            reviewerCounts[reviewerKey!] = (1, displayName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    [WARNING] Failed to analyze PR #{pr.PullRequestId}: {ex.Message}");
                    }
                }

                Console.WriteLine($"[INFO] Found {apiReviewPrsFound} API review PRs in {_adoConfig.RepositoryName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to access repository {_adoConfig.RepositoryName}: {ex.Message}");
            }

            Console.WriteLine($"\n[OK] Analysis complete, found {reviewerCounts.Count} unique API reviewers from API review PRs");
            Console.WriteLine();

            // Sort by frequency
            var sortedReviewers = reviewerCounts
                .OrderByDescending(kvp => kvp.Value.count)
                .ToList();

            Console.WriteLine("[INFO] Required reviewers found:");
            foreach (var (reviewer, (count, displayName)) in sortedReviewers) 
            {
                Console.WriteLine($"  {reviewer} - Required in {count} PRs");
            }

            // Generate JSON configuration
            Console.WriteLine();
            Console.WriteLine("[INFO] Generating JSON configuration...");
            await GenerateJsonConfigurationAsync(sortedReviewers);

            Console.WriteLine("[OK] Analysis complete!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
            throw;
        }
    }

    private static string? GetReviewerKey(IdentityRefWithVote reviewer)
    {
        // Try to extract a meaningful identifier
        if (!string.IsNullOrEmpty(reviewer.UniqueName))
        {
            // If it's an email, check if it's a real person (not a service account)
            if (reviewer.UniqueName.Contains("@microsoft.com"))
            {
                var email = reviewer.UniqueName.ToLowerInvariant();

                // Filter out automated service accounts and tools
                if (email.Contains("enforcer") ||          // esownenf@microsoft.com (Ownership Enforcer)
                    email.Equals("esownenf@microsoft.com") || // Explicit block for ownership enforcer
                    email.Contains("bot") ||               // Any bot accounts
                    email.Contains("service") ||           // Service accounts
                    email.Contains("automation") ||        // Automation accounts
                    email.Contains("system") ||            // System accounts
                    email.Contains("noreply") ||           // No-reply accounts
                    email.Contains("donotreply"))          // Do-not-reply accounts
                {
                    return null; // Skip this reviewer
                }

                return reviewer.UniqueName;
            }

            // Skip VSTFS groups entirely - we only want individual accounts
            if (reviewer.UniqueName.StartsWith("vstfs:"))
            {
                return null; // Skip groups
            }
        }

        // Skip if it's clearly a group or team name
        var displayName = reviewer.DisplayName ?? "";
        if (displayName.ToLowerInvariant().Contains("team") ||
            displayName.ToLowerInvariant().Contains("group") ||
            displayName.ToLowerInvariant().Contains("reviewers") ||
            displayName.Contains("[TEAM FOUNDATION]"))
        {
            return null; // Skip groups
        }

        // For individual accounts without email, fall back to display name if it looks like a person
        return reviewer.DisplayName ?? reviewer.Id?.ToString() ?? "Unknown";
    }

    private static string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Length <= maxLength ? input : input.Substring(0, maxLength - 3) + "...";
    }

    private static async Task GenerateJsonConfigurationAsync(List<KeyValuePair<string, (int count, string displayName)>> sortedReviewers)
    {
        // Filter to only individual email accounts
        var individualReviewers = sortedReviewers
            .Where(kvp => kvp.Key.Contains("@microsoft.com"))
            .Take(25) // Top 25 individual reviewers
            .ToList();

        var reviewers = new List<Models.ApiReviewer>();
        
        if (individualReviewers.Any())
        {
            foreach (var (email, (count, displayName)) in individualReviewers)
            {
                reviewers.Add(new Models.ApiReviewer
                {
                    Email = email,
                    DisplayName = !string.IsNullOrEmpty(displayName) && 
                                 displayName != email && 
                                 !displayName.Equals(email, StringComparison.OrdinalIgnoreCase) && 
                                 !displayName.Contains('@') 
                                 ? displayName 
                                 : "",
                    PullRequestCount = count,
                    LastSeen = DateTime.UtcNow
                });
            }
        }

        var config = new Models.ReviewersConfiguration
        {
            LastUpdated = DateTime.UtcNow,
            Source = "Generated from recent API review pull requests analysis",
            Reviewers = reviewers
        };

        var configService = new ReviewersConfigurationService();
        await configService.SaveConfigurationAsync(config);

        Console.WriteLine();
        Console.WriteLine("[INFO] JSON configuration saved successfully!");
        Console.WriteLine($"[INFO] Configuration contains {reviewers.Count} reviewers");
        Console.WriteLine($"[INFO] Saved to: {configService.GetConfigurationFilePath()}");
        
        // Display the reviewers for confirmation
        Console.WriteLine();
        Console.WriteLine("Generated configuration contains:");
        foreach (var reviewer in reviewers.Take(10))
        {
            var nameInfo = !string.IsNullOrEmpty(reviewer.DisplayName) ? $" ({reviewer.DisplayName})" : "";
            Console.WriteLine($"  - {reviewer.Email}{nameInfo} - {reviewer.PullRequestCount} reviews");
        }
        if (reviewers.Count > 10)
        {
            Console.WriteLine($"  ... and {reviewers.Count - 10} more reviewers");
        }
    }
}
