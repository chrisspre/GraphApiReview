namespace review;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // PR checker functionality
            await RunPRCheckerAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static async Task RunPRCheckerAsync()
    {
        // Azure DevOps organization URL
        var organizationUrl = "https://msazure.visualstudio.com/";

        // Project and repository details
        var projectName = "One";
        var repositoryName = "AD-AggregatorService-Workloads";

        Console.WriteLine("gapir (Graph API Review) - Azure DevOps Pull Request Checker");
        Console.WriteLine("===============================================================");
        // Console.WriteLine("Full URLs: https://msazure.visualstudio.com/One/_git/AD-AggregatorService-Workloads/pullrequest/{ID}");
        Console.WriteLine();

        // Authenticate using Visual Studio credentials or prompt for PAT
        var connection = await ConsoleAuth.AuthenticateAsync(organizationUrl);

        if (connection == null)
        {
            Console.WriteLine("Authentication failed. Exiting...");
            return;
        }

        Console.WriteLine("Successfully authenticated!");

        // Get pull requests assigned to the current user
        await CheckPullRequestsAsync(connection, projectName, repositoryName);
    }

    static async Task CheckPullRequestsAsync(VssConnection connection, string projectName, string repositoryName)
    {
        try
        {
            // Get Git client
            var gitClient = connection.GetClient<GitHttpClient>();

            // Get current user identity
            var currentUser = connection.AuthorizedIdentity;

            Console.WriteLine($"Checking pull requests for user: {currentUser.DisplayName}");

            // Get repository
            var repository = await gitClient.GetRepositoryAsync(projectName, repositoryName);

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

            // Show short list of approved PRs
            if (approvedPullRequests.Count > 0)
            {
                Console.WriteLine($"\n✅ {approvedPullRequests.Count} PR(s) you have already approved:");

                // Prepare table data
                var headers = new[] { "Author", "Title", "URL" };
                var maxWidths = new[] { 25, 45, -1 }; // Maximum column widths, -1 means no limit for URLs

                var rows = new List<string[]>();
                foreach (var pr in approvedPullRequests)
                {
                    var url = GetFullPullRequestUrl(pr, projectName, repositoryName);

                    rows.Add([pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), url]);
                }

                PrintTable(headers, rows, maxWidths);
            }

            // Show detailed list of pending PRs
            Console.WriteLine($"\n⏳ {pendingPullRequests.Count} PR(s) pending your approval:");
            Console.WriteLine(new string('=', 60));

            if (pendingPullRequests.Count == 0)
            {
                Console.WriteLine("No pull requests found pending your approval.");
                Console.WriteLine();
                return;
            }

            foreach (var pr in pendingPullRequests)
            {
                Console.WriteLine($"ID: {pr.PullRequestId}");
                Console.WriteLine($"Title: {ShortenTitle(pr.Title)}");
                Console.WriteLine($"Author: {pr.CreatedBy.DisplayName}");
                Console.WriteLine($"Status: {pr.Status}");
                Console.WriteLine($"Created: {pr.CreationDate:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"URL: {GetFullPullRequestUrl(pr, projectName, repositoryName)}");

                // Check if there are any reviewers (filter out groups and automation accounts)
                if (pr.Reviewers?.Any() == true)
                {
                    var humanReviewers = pr.Reviewers.Where(r =>
                        !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                        !r.DisplayName.StartsWith("[One]") &&
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

    static string GetFullPullRequestUrl(GitPullRequest pr, string projectName, string repositoryName)
    {
        return $"https://msazure.visualstudio.com/{projectName}/_git/{repositoryName}/pullrequest/{pr.PullRequestId}";
    }

    static string ShortenTitle(string title)
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

    static void PrintTable(string[] headers, IReadOnlyCollection<string[]> rows, int[] maxWidths)
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

    static string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (text.Length <= maxLength)
            return text;

        return $"{text[..(maxLength - 3)]}...";
    }
}
