using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System.Text;

namespace gapir;

public class ReviewerCollector
{
    private const string Organization = "https://dev.azure.com/msazure";
    private const string Project = "One";
    private const string Repository = "AD-AggregatorService-Workloads";
    private const int MaxPrsToAnalyze = 50;

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

            Console.WriteLine($"[INFO] Fetching recent pull requests (limit: {MaxPrsToAnalyze})...");
            
            // Get recent completed PRs
            var prs = await gitClient.GetPullRequestsAsync(
                project: Project,
                repositoryId: Repository,
                searchCriteria: new GitPullRequestSearchCriteria
                {
                    Status = PullRequestStatus.Completed
                },
                top: MaxPrsToAnalyze);

            Console.WriteLine($"[OK] Found {prs.Count} completed pull requests");
            Console.WriteLine("[INFO] Analyzing required reviewers...");

            var reviewerCounts = new Dictionary<string, (int count, string displayName)>();

            foreach (var pr in prs)
            {
                Console.WriteLine($"  Analyzing PR #{pr.PullRequestId}: {TruncateString(pr.Title, 50)}...");
                
                try
                {
                    // Get detailed PR with reviewers
                    var detailedPr = await gitClient.GetPullRequestAsync(
                        project: Project,
                        repositoryId: Repository,
                        pullRequestId: pr.PullRequestId);

                    if (detailedPr?.Reviewers != null)
                    {
                        foreach (var reviewer in detailedPr.Reviewers)
                        {
                            // Consider a reviewer "required" if they are marked as required OR they provided approval/feedback
                            if (reviewer.IsRequired == true || reviewer.Vote > 0)
                            {
                                var reviewerKey = GetReviewerKey(reviewer);
                                if (!string.IsNullOrEmpty(reviewerKey))
                                {
                                    var displayName = reviewer.DisplayName ?? reviewerKey;
                                    if (reviewerCounts.ContainsKey(reviewerKey))
                                    {
                                        reviewerCounts[reviewerKey] = (reviewerCounts[reviewerKey].count + 1, displayName);
                                    }
                                    else
                                    {
                                        reviewerCounts[reviewerKey] = (1, displayName);
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

            Console.WriteLine($"[OK] Analyzed {prs.Count} PRs, found {reviewerCounts.Count} unique required reviewers");
            Console.WriteLine();

            // Sort by frequency
            var sortedReviewers = reviewerCounts
                .OrderByDescending(kvp => kvp.Value.count)
                .ToList();

            Console.WriteLine("[INFO] Required reviewers found:");
            foreach (var (reviewer, (count, displayName)) in sortedReviewers.Take(20)) // Show top 20
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

    private static string GetReviewerKey(IdentityRefWithVote reviewer)
    {
        // Try to extract a meaningful identifier
        if (!string.IsNullOrEmpty(reviewer.UniqueName))
        {
            // If it's an email, use that
            if (reviewer.UniqueName.Contains("@microsoft.com"))
            {
                return reviewer.UniqueName;
            }
            
            // If it's a VSTFS group identifier, use the display name
            if (reviewer.UniqueName.StartsWith("vstfs:") && !string.IsNullOrEmpty(reviewer.DisplayName))
            {
                return reviewer.DisplayName;
            }
        }

        // Fall back to display name
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
        sb.AppendLine($"// Based on analysis of recent pull requests");
        sb.AppendLine();
        sb.AppendLine("public static readonly HashSet<string> KnownApiReviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("{");

        // Add email addresses and clean identifiers
        var emailReviewers = new List<string>();
        var groupReviewers = new List<string>();
        
        foreach (var (reviewer, (count, displayName)) in sortedReviewers)
        {
            // Filter to focus on likely API reviewers
            if (reviewer.Contains("@microsoft.com"))
            {
                // Add display name if it's different from email and meaningful
                var nameComment = "";
                if (!string.IsNullOrEmpty(displayName) && displayName != reviewer && !displayName.Equals(reviewer, StringComparison.OrdinalIgnoreCase))
                {
                    nameComment = $" ({displayName})";
                }
                emailReviewers.Add($"    \"{reviewer}\", // Required in {count} PRs{nameComment}");
            }
            else if (reviewer.Contains("API") || reviewer.Contains("Graph") || reviewer.Contains("reviewers"))
            {
                // Try to extract meaningful group names
                var cleanName = reviewer.Replace("[TEAM FOUNDATION]\\", "");
                groupReviewers.Add($"    // Group: {cleanName} - Required in {count} PRs");
            }
        }

        // Add top email reviewers
        sb.AppendLine("    // Individual reviewers (email addresses)");
        foreach (var email in emailReviewers.Take(20))
        {
            sb.AppendLine(email);
        }

        sb.AppendLine();
        sb.AppendLine("    // Note: Group reviewers found but not included (they need to be resolved to individual members):");
        foreach (var group in groupReviewers.Take(10))
        {
            sb.AppendLine(group);
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
