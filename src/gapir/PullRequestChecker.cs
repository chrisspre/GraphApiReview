namespace gapir;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using gapir.Utilities;

public class PullRequestChecker(bool showApproved, bool useShortUrls = true, bool showDetailedTiming = false)
{
    private readonly bool _showApproved = showApproved;
    private readonly bool _useShortUrls = useShortUrls;
    private readonly bool _showDetailedTiming = showDetailedTiming;

    // Azure DevOps organization URL, Project and repository details
    private const string OrganizationUrl = "https://msazure.visualstudio.com/";
    private const string ProjectName = "One";
    private const string RepositoryName = "AD-AggregatorService-Workloads";

    public async Task RunAsync()
    {
        Console.WriteLine("gapir (Graph API Review) - Azure DevOps Pull Request Checker");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        var connection = await ConsoleAuth.AuthenticateAsync(OrganizationUrl);

        if (connection == null)
        {
            Console.WriteLine("Authentication failed. Exiting...");
            return;
        }

        Log.Information("Successfully authenticated!");

        // Get pull requests assigned to the current user
        await CheckPullRequestsAsync(connection);
    }

    private async Task CheckPullRequestsAsync(VssConnection connection)
    {
        try
        {
            // Get Git client
            var gitClient = connection.GetClient<GitHttpClient>();

            // Get current user identity
            var currentUser = connection.AuthorizedIdentity;

            Log.Information($"Checking pull requests for user: {currentUser.DisplayName}");

            // Get repository
            var repository = await gitClient.GetRepositoryAsync(ProjectName, RepositoryName);

            // Search for pull requests assigned to the current user
            var searchCriteria = new GitPullRequestSearchCriteria()
            {
                Status = PullRequestStatus.Active,
                ReviewerId = currentUser.Id
            };

            var pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);

            // Separate PRs into approved and pending
            var approvedPullRequests = new List<GitPullRequest>();
            var pendingPullRequests = new List<GitPullRequest>();

            foreach (var pr in pullRequests)
            {
                var currentUserReviewer = pr.Reviewers?.FirstOrDefault(r =>
                    r.Id.Equals(currentUser.Id) ||
                    r.DisplayName.Equals(currentUser.DisplayName, StringComparison.OrdinalIgnoreCase));

                var isApproved = currentUserReviewer != null && currentUserReviewer.Vote == 10;

                if (isApproved)
                    approvedPullRequests.Add(pr);
                else
                    pendingPullRequests.Add(pr);
            }

            // Show short list of approved PRs (only if requested)
            if (_showApproved && approvedPullRequests.Count > 0)
            {
                Console.WriteLine($"\n✅ {approvedPullRequests.Count} PR(s) you have already approved:");
                Console.WriteLine("Reason why PR is not completed: ");
                Console.WriteLine("    REJ=Rejected, WFA=Waiting For Author, PAP=Pending Approvals, POL=Policy/Build Issues");
                
                if (_showDetailedTiming)
                {
                    Console.WriteLine("Age columns: MyAge=Days since your approval, AllAge=Days since all required approvals (- if not all approved)");
                }

                // Prepare table data - adjust headers based on detailed timing flag
                string[] headers;
                int[] maxWidths;
                
                if (_showDetailedTiming)
                {
                    headers = new[] { "Author", "Title", "Why", "MyAge", "AllAge", "URL" };
                    maxWidths = new[] { 20, 30, 3, 5, 6, -1 }; // Adjusted widths for additional columns
                }
                else
                {
                    headers = new[] { "Author", "Title", "Why", "URL" };
                    maxWidths = new[] { 25, 40, 8, -1 };
                }

                var rows = new List<string[]>();
                foreach (var pr in approvedPullRequests)
                {
                    var url = GetFullPullRequestUrl(pr, _useShortUrls);
                    var reason = GetPendingReason(pr);

                    if (_showDetailedTiming)
                    {
                        var myApprovalAge = await GetMyApprovalAge(gitClient, pr, currentUser.Id);
                        var allApprovalAge = await GetAllApprovalsAge(gitClient, pr);
                        
                        rows.Add([pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, myApprovalAge, allApprovalAge, url]);
                    }
                    else
                    {
                        rows.Add([pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, url]);
                    }
                }

                PrintTable(headers, rows, maxWidths);
            }

            // Show detailed list of pending PRs
            Console.WriteLine($"\n⏳ {pendingPullRequests.Count} PR(s) pending your approval:");

            if (pendingPullRequests.Count == 0)
            {
                Console.WriteLine("No pull requests found pending your approval.");
                Console.WriteLine();
                return;
            }

            // Prepare table data for pending PRs
            var pendingHeaders = new[] { "Title", "Author", "Assigned", "Ratio", "ID", "URL" };
            var pendingMaxWidths = new[] { 35, 20, 12, 12, 8, -1 }; // -1 means no limit for URLs

            var pendingRows = new List<string[]>();
            foreach (var pr in pendingPullRequests)
            {
                var timeAssigned = GetTimeAssignedToReviewer(pr, currentUser.Id);
                var approvalRatio = GetApprovalRatio(pr);
                var url = GetFullPullRequestUrl(pr, _useShortUrls);

                pendingRows.Add([
                    ShortenTitle(pr.Title),
                    pr.CreatedBy.DisplayName,
                    timeAssigned,
                    approvalRatio,
                    pr.PullRequestId.ToString(),
                    url
                ]);
            }

            PrintTable(pendingHeaders, pendingRows, pendingMaxWidths);

            // Show detailed information for each pending PR
            Console.WriteLine($"\n📋 Detailed information:");
            Console.WriteLine(new string('=', 60));

            foreach (var pr in pendingPullRequests)
            {
                Console.WriteLine($"ID: {pr.PullRequestId}");
                Console.WriteLine($"Title: {ShortenTitle(pr.Title)}");
                Console.WriteLine($"Author: {pr.CreatedBy.DisplayName}");
                Console.WriteLine($"Status: {pr.Status}");
                Console.WriteLine($"Created: {pr.CreationDate:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"URL: {GetFullPullRequestUrl(pr, _useShortUrls)}");

                // Check if there are any reviewers (filter out groups and automation accounts)
                if (pr.Reviewers?.Any() == true)
                {
                    var humanReviewers = pr.Reviewers.Where(r =>
                        !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                        !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                        !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                        !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                        !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
                    ).ToList();

                    if (humanReviewers.Any())
                    {
                        Console.WriteLine("Reviewers:");
                        foreach (var reviewer in humanReviewers)
                        {
                            var vote = reviewer.Vote switch
                            {
                                10 => "Approved",
                                5 => "Approved with suggestions",
                                0 => "No vote",
                                -5 => "Waiting for author",
                                -10 => "Rejected",
                                _ => "Unknown"
                            };
                            Console.WriteLine($"  - {reviewer.DisplayName}: {vote}");
                        }
                    }
                }

                Console.WriteLine(new string('-', 60));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking pull requests: {ex.Message}");
        }
    }

    private static string GetFullPullRequestUrl(GitPullRequest pr, bool useShortUrls)
    {
        if (useShortUrls)
        {
            // Use Base62 encoding for shorter URLs
            string base62Id = Base62.Encode(pr.PullRequestId);
            return $"http://g/pr/{base62Id}";
        }
        else
        {
            return $"https://msazure.visualstudio.com/{ProjectName}/_git/{RepositoryName}/pullrequest/{pr.PullRequestId}";
        }
    }

    private static string ShortenTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return title;

        // Remove all dashes and replace with spaces
        var cleaned = title.Replace("-", " ");

        // Remove trailing numbers (like issue numbers)
        // Pattern: removes numbers at the end, optionally preceded by spaces or special chars
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^\s*\d+\s*", "");

        // Remove extra whitespace
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

        // Truncate to 40 characters
        if (cleaned.Length > 40)
        {
            cleaned = $"{cleaned[..37]}...";
        }

        return cleaned;
    }

    private static void PrintTable(string[] headers, IReadOnlyCollection<string[]> rows, int[] maxWidths)
    {
        if (headers == null || rows == null || maxWidths == null)
            return;

        if (headers.Length != maxWidths.Length)
            throw new ArgumentException("Headers and maxWidths arrays must have the same length.");

        // Calculate actual column widths based on content
        var actualWidths = new int[headers.Length];

        // Start with header lengths
        for (int i = 0; i < headers.Length; i++)
        {
            actualWidths[i] = maxWidths[i] == -1 ? headers[i].Length : Math.Min(headers[i].Length, maxWidths[i]);
        }

        // Check content lengths
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows

            for (int i = 0; i < row.Length && i < actualWidths.Length; i++)
            {
                var contentLength = row[i]?.Length ?? 0;
                if (maxWidths[i] == -1)
                {
                    // No limit for this column
                    actualWidths[i] = Math.Max(actualWidths[i], contentLength);
                }
                else
                {
                    actualWidths[i] = Math.Max(actualWidths[i], Math.Min(contentLength, maxWidths[i]));
                }
            }
        }

        // Print header
        Console.WriteLine();
        for (int i = 0; i < headers.Length; i++)
        {
            var header = maxWidths[i] == -1 ? headers[i] : TruncateString(headers[i], actualWidths[i]);
            Console.Write($"{header.PadRight(actualWidths[i])}");
            if (i < headers.Length - 1)
                Console.Write(" | ");
        }
        Console.WriteLine();

        // Print separator
        for (int i = 0; i < headers.Length; i++)
        {
            Console.Write(new string('-', actualWidths[i]));
            if (i < headers.Length - 1)
                Console.Write("-+-");
        }
        Console.WriteLine();

        // Print rows
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows

            for (int i = 0; i < row.Length; i++)
            {
                var content = maxWidths[i] == -1 ? (row[i] ?? "") : TruncateString(row[i] ?? "", actualWidths[i]);
                Console.Write($"{content.PadRight(actualWidths[i])}");
                if (i < row.Length - 1)
                    Console.Write(" | ");
            }
            Console.WriteLine();
        }
    }

    private static string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return $"{text[..(maxLength - 3)]}...";
    }

    private static string GetTimeAssignedToReviewer(GitPullRequest pr, Guid reviewerId)
    {
        try
        {
            // Since Azure DevOps API doesn't directly provide reviewer assignment time,
            // we'll use the PR creation date as the baseline for when reviewers were assigned
            var timeSinceCreation = DateTime.UtcNow - pr.CreationDate;
            return FormatTimeDifference(timeSinceCreation);
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetApprovalRatio(GitPullRequest pr)
    {
        try
        {
            if (pr.Reviewers?.Length == 0) { return "0/0"; }

            // Filter out system accounts and get human reviewers only
            var humanReviewers = pr.Reviewers?.Where(r =>
                !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var totalReviewers = humanReviewers?.Count;
            var approvedCount = humanReviewers?.Count(r => r.Vote >= 5); // 5 = approved with suggestions, 10 = approved

            return $"{approvedCount}/{totalReviewers}";
        }
        catch
        {
            return "?/?";
        }
    }

    private static string FormatTimeDifference(TimeSpan timeDiff)
    {
        if (timeDiff.TotalDays >= 1)
            return $"{(int)timeDiff.TotalDays}d";
        else if (timeDiff.TotalHours >= 1)
            return $"{(int)timeDiff.TotalHours}h";
        else if (timeDiff.TotalMinutes >= 1)
            return $"{(int)timeDiff.TotalMinutes}m";
        else
            return "< 1m";
    }

    private static string GetPendingReason(GitPullRequest pr)
    {
        try
        {
            // Check if there are any reviewers and get human reviewers only
            var humanReviewers = pr.Reviewers?.Where(r =>
                !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (humanReviewers?.Any() == true)
            {
                // Check for rejections first (highest priority)
                var rejectedCount = humanReviewers.Count(r => r.Vote == -10);
                if (rejectedCount > 0)
                    return "REJ"; // Rejected

                // Check for waiting for author
                var waitingForAuthorCount = humanReviewers.Count(r => r.Vote == -5);
                if (waitingForAuthorCount > 0)
                    return "WFA"; // Waiting For Author

                // Check approval ratio
                var approvedCount = humanReviewers.Count(r => r.Vote >= 5); // 5+ = approved
                var totalReviewers = humanReviewers.Count;
                
                if (approvedCount < totalReviewers)
                    return "PAP"; // Pending Approvals
            }

            // Check for merge conflicts (this would need additional API calls to be certain)
            // For now, we'll assume if none of the above, it might be a build or policy issue
            return "POL"; // Policy/Build issues (could be build failure, branch policy, etc.)
        }
        catch
        {
            return "UNK"; // Unknown
        }
    }

    private static async Task<string> GetMyApprovalAge(GitHttpClient gitClient, GitPullRequest pr, Guid currentUserId)
    {
        try
        {
            if (!currentUserId.Equals(Guid.Empty))
            {
                // Get PR timeline to find when current user approved
                var prThreads = await gitClient.GetThreadsAsync(pr.Repository.Id, pr.PullRequestId);
                
                // Look through threads for vote changes by current user
                DateTime? approvalDate = null;
                
                foreach (var thread in prThreads)
                {
                    if (thread.Comments?.Any() == true)
                    {
                        foreach (var comment in thread.Comments)
                        {
                            // Check if this comment represents an approval vote by current user
                            if (comment.Author?.Id.Equals(currentUserId) == true && 
                                comment.CommentType == CommentType.System &&
                                comment.Content?.Contains("approved", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                if (approvalDate == null || comment.PublishedDate > approvalDate)
                                {
                                    approvalDate = comment.PublishedDate;
                                }
                            }
                        }
                    }
                }
                
                if (approvalDate.HasValue)
                {
                    var timeDiff = DateTime.UtcNow - approvalDate.Value;
                    return FormatTimeDifferenceDays(timeDiff);
                }
            }
            
            // Fallback to PR creation date if we can't find specific approval date
            var fallbackDiff = DateTime.UtcNow - pr.CreationDate;
            return FormatTimeDifferenceDays(fallbackDiff);
        }
        catch
        {
            return "?";
        }
    }

    private static async Task<string> GetAllApprovalsAge(GitHttpClient gitClient, GitPullRequest pr)
    {
        try
        {
            // Check if all required human reviewers have approved
            var humanReviewers = pr.Reviewers?.Where(r =>
                !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
            ).ToList();
            
            if (humanReviewers?.Any() != true)
            {
                return "-";
            }
            
            var approvedCount = humanReviewers.Count(r => r.Vote >= 5);
            var totalRequired = humanReviewers.Count;
            
            if (approvedCount < totalRequired)
            {
                return "-"; // Not all approved yet
            }
            
            // Get PR timeline to find when the last required approval happened
            var prThreads = await gitClient.GetThreadsAsync(pr.Repository.Id, pr.PullRequestId);
            
            DateTime? lastApprovalDate = null;
            
            foreach (var thread in prThreads)
            {
                if (thread.Comments?.Any() == true)
                {
                    foreach (var comment in thread.Comments)
                    {
                        // Check if this comment represents an approval vote
                        if (comment.CommentType == CommentType.System &&
                            comment.Content?.Contains("approved", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            if (lastApprovalDate == null || comment.PublishedDate > lastApprovalDate)
                            {
                                lastApprovalDate = comment.PublishedDate;
                            }
                        }
                    }
                }
            }
            
            if (lastApprovalDate.HasValue)
            {
                var timeDiff = DateTime.UtcNow - lastApprovalDate.Value;
                return FormatTimeDifferenceDays(timeDiff);
            }
            
            // Fallback to PR creation date
            var fallbackDiff = DateTime.UtcNow - pr.CreationDate;
            return FormatTimeDifferenceDays(fallbackDiff);
        }
        catch
        {
            return "?";
        }
    }

    private static string FormatTimeDifferenceDays(TimeSpan timeDiff)
    {
        // For age columns, we only show days to keep it compact
        if (timeDiff.TotalDays >= 1)
            return $"{(int)timeDiff.TotalDays}d";
        else
            return "< 1d";
    }
}
