using Microsoft.TeamFoundation.SourceControl.WebApi;
using System.Text;

namespace gapir;

public class ReviewerCollector
{
    private const string Organization = "https://dev.azure.com/msazure";
    private const string Project = "One";

    // Repository to analyze for API reviewers
    private const string RepositoryToAnalyze = "AD-AggregatorService-Workloads";

    private const int MaxPrsToAnalyze = 500;

    public async Task CollectAndGenerateAsync()
    {
        Console.WriteLine("Pull Request Required Reviewers Analyzer");
        Console.WriteLine(new string('=', 50));

        try
        {
            // Get authenticated connection
            using var connection = await ConsoleAuth.AuthenticateAsync(Organization);
            if (connection == null)
            {
                Console.WriteLine("[ERROR] Authentication failed.");
                return;
            }

            var gitClient = connection.GetClient<GitHttpClient>();
            var reviewerCounts = new Dictionary<string, (int count, string displayName)>();

            Console.WriteLine($"\n[INFO] Analyzing repository: {RepositoryToAnalyze}");
            Console.WriteLine($"[INFO] Fetching recent pull requests (limit: {MaxPrsToAnalyze})...");

            try
            {
                // Get recent completed PRs
                var prs = await gitClient.GetPullRequestsAsync(
                    project: Project,
                    repositoryId: RepositoryToAnalyze,
                    searchCriteria: new GitPullRequestSearchCriteria
                    {
                        Status = PullRequestStatus.Completed
                    },
                    top: MaxPrsToAnalyze);

                Console.WriteLine($"[OK] Found {prs.Count} completed pull requests in {RepositoryToAnalyze}");

                if (prs.Count == 0)
                {
                    Console.WriteLine($"[SKIP] No PRs found in {RepositoryToAnalyze}, skipping...");
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
                            project: Project,
                            repositoryId: RepositoryToAnalyze,
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
                            Console.WriteLine($"  API Review PR #{pr.PullRequestId}: {TruncateString(pr.Title, 50)}...");

                            foreach (var reviewer in detailedPr.Reviewers)
                            {
                                // // Consider a reviewer "required" if they are marked as required OR they provided approval/feedback
                                // if (reviewer.IsRequired == true || reviewer.Vote > 0)
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

                Console.WriteLine($"[INFO] Found {apiReviewPrsFound} API review PRs in {RepositoryToAnalyze}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to access repository {RepositoryToAnalyze}: {ex.Message}");
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

            // Generate C# code
            Console.WriteLine();
            Console.WriteLine("[INFO] Generating C# fallback code...");
            GenerateCSharpCode(sortedReviewers);

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

    private static void GenerateCSharpCode(List<KeyValuePair<string, (int count, string displayName)>> sortedReviewers)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// Generated C# code for ApiReviewersFallback.cs");
        sb.AppendLine($"// Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Based on analysis of recent API review pull requests (PRs with 'Microsoft Graph API reviewers' group assigned)");
        sb.AppendLine();
        sb.AppendLine("public static readonly HashSet<string> KnownApiReviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("{");

        // Filter to only individual email accounts
        var individualReviewers = sortedReviewers
            .Where(kvp => kvp.Key.Contains("@microsoft.com"))
            .Take(25) // Top 25 individual reviewers
            .ToList();

        if (individualReviewers.Any())
        {
            sb.AppendLine("    // Individual reviewers (email addresses only - no groups or service accounts)");
            foreach (var (reviewer, (count, displayName)) in individualReviewers)
            {
                // Add display name if it's different from email and meaningful
                var nameComment = "";
                if (!string.IsNullOrEmpty(displayName) &&
                    displayName != reviewer &&
                    !displayName.Equals(reviewer, StringComparison.OrdinalIgnoreCase) &&
                    !displayName.Contains('@'))
                {
                    nameComment = $"{displayName} ({count})";
                }
                sb.AppendLine($"    \"{reviewer}\", // {nameComment}");
            }
        }
        else
        {
            sb.AppendLine("    // No individual reviewers found");
        }

        sb.AppendLine("};");

        Console.WriteLine();
        Console.WriteLine("Copy the following code to update ApiReviewersFallback.cs:");
        Console.WriteLine(new string('-', 70));
        Console.WriteLine(sb.ToString());
        Console.WriteLine(new string('-', 70));

        // Also save to a file for convenience
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "generated-reviewers.cs");
        File.WriteAllText(outputPath, sb.ToString());
        Console.WriteLine($"[INFO] Code also saved to: {outputPath}");
    }
}
