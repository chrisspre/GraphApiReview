namespace gapir;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using gapir.Utilities;

public class PullRequestChecker(PullRequestCheckerOptions options)
{
    private readonly PullRequestCheckerOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    
    // Cache for API reviewers group members to avoid repeated API calls
    private HashSet<string>? _apiReviewersMembers;

    // Azure DevOps organization URL, Project and repository details
    private const string OrganizationUrl = "https://msazure.visualstudio.com/";
    private const string ProjectName = "One";
    private const string RepositoryName = "AD-AggregatorService-Workloads";
    private const string ApiReviewersGroupName = "[TEAM FOUNDATION]\\SCIM API reviewers";

    public async Task RunAsync()
    {
        Console.WriteLine("gapir (Graph API Review) - Azure DevOps Pull Request Checker");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        VssConnection? connection;
        
        // Authentication phase
        using (var authSpinner = new Spinner("Authenticating with Azure DevOps..."))
        {
            connection = await ConsoleAuth.AuthenticateAsync(OrganizationUrl);

            if (connection == null)
            {
                authSpinner.Error("Authentication failed");
                return;
            }
            
            authSpinner.Success("Authentication successful");
        }

        Log.Information("Successfully authenticated!");

        // Get pull requests assigned to the current user
        await CheckPullRequestsAsync(connection);
    }

    private async Task<HashSet<string>> GetApiReviewersGroupMembersAsync(VssConnection connection)
    {
        if (_apiReviewersMembers != null)
            return _apiReviewersMembers;

        try
        {
            var identityClient = connection.GetClient<IdentityHttpClient>();
            
            Log.Information("Fetching API reviewers group members...");
            
            // Method 1: Try to find the group by searching for it
            var searchResults = await identityClient.ReadIdentitiesAsync(
                IdentitySearchFilter.General,
                ApiReviewersGroupName,
                queryMembership: QueryMembership.Expanded);

            var apiGroup = searchResults?.FirstOrDefault(i => 
                i.DisplayName?.Equals(ApiReviewersGroupName, StringComparison.OrdinalIgnoreCase) == true ||
                i.DisplayName?.Contains("SCIM API reviewers", StringComparison.OrdinalIgnoreCase) == true);

            if (apiGroup != null)
            {
                Log.Debug($"Found group: {apiGroup.DisplayName}, Id: {apiGroup.Id}");
                Log.Debug($"Group MemberIds count: {apiGroup.MemberIds?.Count() ?? 0}");
                Log.Debug($"Group Members count: {apiGroup.Members?.Count() ?? 0}");
                
                // Method 1a: Try MemberIds first (direct members)
                if (apiGroup.MemberIds?.Any() == true)
                {
                    _apiReviewersMembers = apiGroup.MemberIds.Select(id => id.ToString()).ToHashSet();
                    Log.Information($"Found {_apiReviewersMembers.Count} members via MemberIds");
                }
                // Method 1b: Try Members property (might contain nested groups)
                else if (apiGroup.Members?.Any() == true)
                {
                    _apiReviewersMembers = apiGroup.Members.Select(m => m.Identifier).ToHashSet();
                    Log.Information($"Found {_apiReviewersMembers.Count} members via Members property");
                }
                // Method 1c: Try to get group membership by reading the group directly with expanded membership
                else
                {
                    Log.Debug("Group found but no direct members, trying to read group with explicit membership expansion");
                    var groupWithMembers = await identityClient.ReadIdentityAsync(
                        apiGroup.Id, 
                        QueryMembership.Expanded);
                    
                    if (groupWithMembers?.MemberIds?.Any() == true)
                    {
                        _apiReviewersMembers = groupWithMembers.MemberIds.Select(id => id.ToString()).ToHashSet();
                        Log.Information($"Found {_apiReviewersMembers.Count} members via direct group read");
                    }
                    else if (groupWithMembers?.Members?.Any() == true)
                    {
                        _apiReviewersMembers = groupWithMembers.Members.Select(m => m.Identifier).ToHashSet();
                        Log.Information($"Found {_apiReviewersMembers.Count} members via direct group read (Members property)");
                    }
                    else
                    {
                        // Method 1d: Try to get members by searching for users in this group
                        Log.Debug("Trying to find group members by searching for users with this group membership");
                        await TryGetMembersByGroupSearch(identityClient, apiGroup.Id);
                    }
                }
            }
            
            // Method 2: If still no members, try alternative search approaches
            if ((_apiReviewersMembers?.Count ?? 0) == 0)
            {
                await TryAlternativeGroupSearches(identityClient);
            }
            
            // Method 3: Last resort - analyze recent PR reviewers to identify API reviewers
            if ((_apiReviewersMembers?.Count ?? 0) == 0)
            {
                Log.Warning("Could not find API reviewers group members via Identity API");
                Log.Information("Attempting to identify API reviewers from recent PR history...");
                await TryIdentifyApiReviewersFromPRHistory(connection);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching API reviewers group members: {ex.Message}");
            Log.Warning("Falling back to heuristic-based API reviewer detection");
            _apiReviewersMembers = new HashSet<string>();
        }

        return _apiReviewersMembers ?? new HashSet<string>();
    }

    private async Task TryGetMembersByGroupSearch(IdentityHttpClient identityClient, Guid groupId)
    {
        try
        {
            // Azure DevOps Identity API doesn't have ReadMembersOfAsync, let's try a different approach
            // We'll search for the group again with a different query membership approach
            var groupIdentity = await identityClient.ReadIdentityAsync(groupId, QueryMembership.Direct);
            
            if (groupIdentity?.MemberIds?.Any() == true)
            {
                _apiReviewersMembers = groupIdentity.MemberIds.Select(u => u.ToString()).ToHashSet();
                Log.Information($"Found {_apiReviewersMembers.Count} members via ReadIdentityAsync with Direct membership");
            }
            else if (groupIdentity?.Members?.Any() == true)
            {
                _apiReviewersMembers = groupIdentity.Members.Select(m => m.Identifier).ToHashSet();
                Log.Information($"Found {_apiReviewersMembers.Count} members via ReadIdentityAsync Direct (Members property)");
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"ReadIdentityAsync with Direct membership failed: {ex.Message}");
        }
    }

    private async Task TryAlternativeGroupSearches(IdentityHttpClient identityClient)
    {
        try
        {
            // Try searching for just "SCIM API reviewers" without the prefix
            var altSearchResults = await identityClient.ReadIdentitiesAsync(
                IdentitySearchFilter.General,
                "SCIM API reviewers",
                queryMembership: QueryMembership.Expanded);

            var altApiGroup = altSearchResults?.FirstOrDefault(i => 
                i.DisplayName?.Contains("SCIM API reviewers", StringComparison.OrdinalIgnoreCase) == true);

            if (altApiGroup?.MemberIds?.Any() == true)
            {
                _apiReviewersMembers = altApiGroup.MemberIds.Select(id => id.ToString()).ToHashSet();
                Log.Information($"Found {_apiReviewersMembers.Count} members via alternative search (MemberIds)");
            }
            else if (altApiGroup?.Members?.Any() == true)
            {
                _apiReviewersMembers = altApiGroup.Members.Select(m => m.Identifier).ToHashSet();
                Log.Information($"Found {_apiReviewersMembers.Count} members via alternative search (Members)");
            }
            
            // Try different search filters
            if ((_apiReviewersMembers?.Count ?? 0) == 0)
            {
                var accountSearchResults = await identityClient.ReadIdentitiesAsync(
                    IdentitySearchFilter.AccountName,
                    "SCIM API reviewers",
                    queryMembership: QueryMembership.Expanded);
                
                var accountApiGroup = accountSearchResults?.FirstOrDefault(i => 
                    i.DisplayName?.Contains("SCIM API reviewers", StringComparison.OrdinalIgnoreCase) == true);
                
                if (accountApiGroup?.MemberIds?.Any() == true)
                {
                    _apiReviewersMembers = accountApiGroup.MemberIds.Select(id => id.ToString()).ToHashSet();
                    Log.Information($"Found {_apiReviewersMembers.Count} members via account name search");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Alternative group searches failed: {ex.Message}");
        }
    }

    private async Task TryIdentifyApiReviewersFromPRHistory(VssConnection connection)
    {
        try
        {
            var gitClient = connection.GetClient<GitHttpClient>();
            var repository = await gitClient.GetRepositoryAsync(ProjectName, RepositoryName);
            
            // Get recent completed PRs to analyze reviewer patterns  
            var recentSearchCriteria = new GitPullRequestSearchCriteria()
            {
                Status = PullRequestStatus.Completed
            };
            
            var recentPRs = await gitClient.GetPullRequestsAsync(repository.Id, recentSearchCriteria, top: 50);
            
            // Filter to only PRs from the last 30 days
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var filteredPRs = recentPRs.Where(pr => pr.CreationDate >= thirtyDaysAgo).ToList();
            
            // Analyze reviewers who frequently appear on PRs and look for patterns
            var reviewerFrequency = new Dictionary<string, int>();
            var reviewerInfo = new Dictionary<string, (string displayName, string uniqueName)>();
            
            foreach (var pr in filteredPRs)
            {
                if (pr.Reviewers?.Any() == true)
                {
                    foreach (var reviewer in pr.Reviewers)
                    {
                        // Skip system accounts
                        if (reviewer.DisplayName.StartsWith("[TEAM FOUNDATION]") ||
                            reviewer.DisplayName.Contains("Bot") ||
                            reviewer.DisplayName.Contains("Automation"))
                            continue;
                            
                        var reviewerId = reviewer.Id.ToString();
                        reviewerFrequency[reviewerId] = reviewerFrequency.GetValueOrDefault(reviewerId, 0) + 1;
                        reviewerInfo[reviewerId] = (reviewer.DisplayName, reviewer.UniqueName);
                    }
                }
            }
            
            // Identify likely API reviewers (appear on many PRs + heuristics)
            var likelyApiReviewers = reviewerFrequency
                .Where(kvp => kvp.Value >= 3) // Appeared on at least 3 PRs
                .Where(kvp => reviewerInfo.ContainsKey(kvp.Key))
                .Where(kvp => 
                {
                    var (displayName, uniqueName) = reviewerInfo[kvp.Key];
                    return IsLikelyApiReviewer(displayName, uniqueName);
                })
                .Select(kvp => kvp.Key)
                .ToHashSet();
            
            if (likelyApiReviewers.Count > 0)
            {
                _apiReviewersMembers = likelyApiReviewers;
                Log.Information($"Identified {_apiReviewersMembers.Count} likely API reviewers from PR history analysis");
                Log.Debug($"API reviewers identified: {string.Join(", ", likelyApiReviewers.Select(id => reviewerInfo[id].displayName))}");
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"PR history analysis failed: {ex.Message}");
        }
    }

    private static bool IsLikelyApiReviewer(string displayName, string uniqueName)
    {
        // Enhanced heuristics to identify API reviewers
        var lowerDisplayName = displayName?.ToLowerInvariant() ?? "";
        var lowerUniqueName = uniqueName?.ToLowerInvariant() ?? "";
        
        // Look for API-related terms in names or emails
        var apiTerms = new[] { "api", "scim", "graph", "review", "architect", "platform" };
        var exclusionTerms = new[] { "test", "automation", "bot", "build" };
        
        // Check if name/email contains API-related terms
        var hasApiTerms = apiTerms.Any(term => 
            lowerDisplayName.Contains(term) || lowerUniqueName.Contains(term));
            
        // Check if name/email contains exclusion terms
        var hasExclusionTerms = exclusionTerms.Any(term => 
            lowerDisplayName.Contains(term) || lowerUniqueName.Contains(term));
        
        return hasApiTerms && !hasExclusionTerms;
    }

    private async Task CheckPullRequestsAsync(VssConnection connection)
    {
        try
        {
            // Get Git client and current user
            var gitClient = connection.GetClient<GitHttpClient>();
            var currentUser = connection.AuthorizedIdentity;

            Log.Information($"Checking pull requests for user: {currentUser.DisplayName}");

            // Pre-load API reviewers group members
            HashSet<string> apiReviewersMembers;
            using (var groupSpinner = new Spinner("Loading API reviewers group..."))
            {
                apiReviewersMembers = await GetApiReviewersGroupMembersAsync(connection);
                groupSpinner.Success($"Loaded {apiReviewersMembers.Count} API reviewers");
            }

            // Get repository and pull requests
            GitRepository repository;
            List<GitPullRequest> pullRequests;
            
            using (var prSpinner = new Spinner("Fetching pull requests..."))
            {
                repository = await gitClient.GetRepositoryAsync(ProjectName, RepositoryName);
                
                var searchCriteria = new GitPullRequestSearchCriteria()
                {
                    Status = PullRequestStatus.Active,
                    ReviewerId = currentUser.Id
                };

                pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
                prSpinner.Success($"Found {pullRequests.Count} assigned pull requests");
            }

            // Process the results
            Console.WriteLine("Analyzing pull request statuses...");
            
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

            Console.WriteLine(); // Empty line before results

            // Show short list of approved PRs (only if requested)
            if (_options.ShowApproved && approvedPullRequests.Count > 0)
            {
                Console.WriteLine($"✓ {approvedPullRequests.Count} PR(s) you have already approved:");
                Console.WriteLine("Reason why PR is not completed: ");
                Console.WriteLine("    REJ=Rejected, WFA=Waiting For Author, POL=Policy/Build Issues");
                Console.WriteLine("    PRA=Pending Reviewer Approval, POA=Pending Other Approvals");
                

                // Prepare table data - adjust headers based on detailed timing flag
                string[] headers;
                int[] maxWidths;
                
                if (_options.ShowDetailedTiming)
                {
                    headers = new[] { "Author", "Title", "Why", "Age", "URL" };
                    maxWidths = new[] { 20, 30, 3, 5, -1 }; // Adjusted widths for single age column
                }
                else
                {
                    headers = new[] { "Author", "Title", "Why", "URL" };
                    maxWidths = new[] { 25, 40, 8, -1 };
                }

                var rows = new List<string[]>();
                foreach (var pr in approvedPullRequests)
                {
                    var url = GetFullPullRequestUrl(pr, _options.UseShortUrls);
                    var reason = GetPendingReason(pr, apiReviewersMembers);

                    if (_options.ShowDetailedTiming)
                    {
                        var age = FormatTimeDifferenceDays(DateTime.UtcNow - pr.CreationDate);
                        
                        rows.Add([pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, age, url]);
                    }
                    else
                    {
                        rows.Add([pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), reason, url]);
                    }
                }

                PrintTable(headers, rows, maxWidths);
            }

            // Show detailed list of pending PRs
            Console.WriteLine($"⏳ {pendingPullRequests.Count} PR(s) pending your full approval:");

            if (pendingPullRequests.Count == 0)
            {
                Console.WriteLine("No pull requests found pending your full approval.");
                Console.WriteLine();
                return;
            }

            // Prepare table data for pending PRs
            var pendingHeaders = new[] { "Title", "Author", "Assigned", "Ratio", "Status", "API", "Last By", "URL" };
            var pendingMaxWidths = new[] { 30, 18, 10, 8, 6, 6, 8, -1 }; // -1 means no limit for URLs

            var pendingRows = new List<string[]>();
            foreach (var pr in pendingPullRequests)
            {
                var timeAssigned = GetTimeAssignedToReviewer(pr, currentUser.Id);
                var approvalRatio = GetApprovalRatio(pr);
                var url = GetFullPullRequestUrl(pr, _options.UseShortUrls);
                var myVoteStatus = GetMyVoteStatus(pr, currentUser.Id, currentUser.DisplayName);
                var apiApprovalRatio = GetApiApprovalRatio(pr, apiReviewersMembers);
                var lastActivity = await GetLastActivityBy(gitClient, repository.Id, pr, currentUser.Id);

                pendingRows.Add([
                    ShortenTitle(pr.Title),
                    pr.CreatedBy.DisplayName,
                    timeAssigned,
                    approvalRatio,
                    myVoteStatus,
                    apiApprovalRatio,
                    lastActivity,
                    url
                ]);
            }

            PrintTable(pendingHeaders, pendingRows, pendingMaxWidths);

            // Show detailed information for each pending PR (only if not hidden)
            if (_options.ShowDetailedInfo)
            {
                Console.WriteLine($"\n📋 Detailed information:");
                Console.WriteLine(new string('=', 80));

                foreach (var pr in pendingPullRequests)
                {
                    Console.WriteLine($"ID: {pr.PullRequestId}");
                    Console.WriteLine($"Title: {ShortenTitle(pr.Title)}");
                    Console.WriteLine($"Author: {pr.CreatedBy.DisplayName}");
                    Console.WriteLine($"Status: {pr.Status}");
                    Console.WriteLine($"Created: {pr.CreationDate:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"URL: {GetFullPullRequestUrl(pr, _options.UseShortUrls)}");

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

                    Console.WriteLine(new string('-', 80));
                }
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

        // Truncate to 30 characters for table display
        if (cleaned.Length > 30)
        {
            cleaned = $"{cleaned[..27]}...";
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

    private static string GetMyVoteStatus(GitPullRequest pr, Guid currentUserId, string currentUserDisplayName)
    {
        try
        {
            var currentUserReviewer = pr.Reviewers?.FirstOrDefault(r =>
                r.Id.Equals(currentUserId) ||
                r.DisplayName.Equals(currentUserDisplayName, StringComparison.OrdinalIgnoreCase));
            
            if (currentUserReviewer == null)
                return "---"; // Not a reviewer
                
            return currentUserReviewer.Vote switch
            {
                 10 => "Apprvd", // Approved
                  5 => "ApSugg", // Approved with suggestions
                  0 => "NoVote", // No vote
                 -5 => "Wait4A", // Waiting for author (you requested changes)
                -10 => "Reject", // Rejected
                _   => "Unknow" // Unknown
            };
        }
        catch
        {
            return "Error"; // Error
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

    private static string GetPendingReason(GitPullRequest pr, HashSet<string> apiReviewersMembers)
    {
        try
        {
            // Check if there are any reviewers and get all reviewers
            var allReviewers = pr.Reviewers?.ToList() ?? new List<IdentityRefWithVote>();
            
            // Check for rejections first (highest priority) - from any reviewer
            var rejectedCount = allReviewers.Count(r => r.Vote == -10);
            if (rejectedCount > 0)
                return "Reject"; // Rejected

            // Check for waiting for author - from any reviewer
            var waitingForAuthorCount = allReviewers.Count(r => r.Vote == -5);
            if (waitingForAuthorCount > 0)
                return "Wait4A"; // Waiting For Author

            // Identify API reviewers using actual group membership (preferred) or heuristics (fallback)
            List<IdentityRefWithVote> apiReviewers;
            List<IdentityRefWithVote> nonApiReviewers;

            if (apiReviewersMembers.Count > 0)
            {
                // Use actual group membership data - convert Guid to string for comparison
                apiReviewers = allReviewers.Where(r => apiReviewersMembers.Contains(r.Id.ToString())).ToList();
                nonApiReviewers = allReviewers.Where(r => 
                    !apiReviewersMembers.Contains(r.Id.ToString()) &&
                    !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                    !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                    !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            else
            {
                // Fallback to heuristic-based detection
                apiReviewers = allReviewers.Where(r => 
                    IsApiReviewer(r.UniqueName, r.DisplayName)).ToList();
                
                nonApiReviewers = allReviewers.Where(r => 
                    !IsApiReviewer(r.UniqueName, r.DisplayName) &&
                    !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                    !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                    !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            // Check API reviewers approval status (need at least 2 approved)
            var apiApprovedCount = apiReviewers.Count(r => r.Vote >= 5);
            var apiRequiredCount = 2; // Policy requires at least 2 API reviewers

            // Check non-API reviewers approval status  
            var nonApiApprovedCount = nonApiReviewers.Count(r => r.Vote >= 5);
            var nonApiTotalCount = nonApiReviewers.Count;

            // If we don't have enough API approvals yet
            if (apiReviewers.Count > 0 && apiApprovedCount < apiRequiredCount)
                return "PendRv"; // Pending Reviewer Approval (API reviewers)

            // If API approvals are satisfied but non-API reviewers haven't all approved
            if (apiApprovedCount >= apiRequiredCount && nonApiTotalCount > 0 && nonApiApprovedCount < nonApiTotalCount)
                return "PendOt"; // Pending Other Approvals

            // If no specific API reviewers found, use general logic
            if (apiReviewers.Count == 0)
            {
                var totalApproved = allReviewers.Count(r => r.Vote >= 5 && 
                    !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                    !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                    !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase));
                
                var totalRequired = allReviewers.Count(r => 
                    !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                    !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                    !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                    !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase));
                
                if (totalApproved < totalRequired)
                    return "PendRv"; // Pending Reviewer Approval (general)
            }

            // If we get here, approvals are likely satisfied but there might be policy/build issues
            return "Policy"; // Policy/Build issues (could be build failure, branch policy, etc.)
        }
        catch
        {
            return "UNK"; // Unknown
        }
    }

    private static bool IsApiReviewer(string uniqueName, string displayName)
    {
        // Heuristic fallback to identify API reviewers when group membership data is not available
        // This method is used only when Azure DevOps Identity API calls fail
        
        // Check if the unique name contains patterns that suggest API reviewer membership
        if (!string.IsNullOrEmpty(uniqueName))
        {
            // Look for known API reviewer email patterns or names
            var lowerUniqueName = uniqueName.ToLowerInvariant();
            
            // Add known patterns here - you would need to update this based on actual team members
            // For now, we'll use a conservative approach
            if (lowerUniqueName.Contains("apireview") || 
                lowerUniqueName.Contains("api-review") ||
                lowerUniqueName.Contains("scim"))
            {
                return true;
            }
        }

        // Check display name patterns
        if (!string.IsNullOrEmpty(displayName))
        {
            var lowerDisplayName = displayName.ToLowerInvariant();
            if (lowerDisplayName.Contains("api review") || 
                lowerDisplayName.Contains("scim"))
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatTimeDifferenceDays(TimeSpan timeDiff)
    {
        // For age columns, we only show days to keep it compact
        if (timeDiff.TotalDays >= 1)
            return $"{(int)timeDiff.TotalDays}d";
        else
            return "< 1d";
    }

    private static string GetApiApprovalRatio(GitPullRequest pr, HashSet<string> apiReviewersMembers)
    {
        try
        {
            if (apiReviewersMembers.Count == 0)
                return "?/?"; // No API reviewers data available

            // Filter to only API reviewers
            var apiReviewers = pr.Reviewers?.Where(r => apiReviewersMembers.Contains(r.Id.ToString())).ToList();
            
            if (apiReviewers == null || apiReviewers.Count == 0)
                return "0/0"; // No API reviewers assigned

            var approvedCount = apiReviewers.Count(r => r.Vote >= 5); // 5 = approved with suggestions, 10 = approved
            var totalCount = apiReviewers.Count;

            return $"{approvedCount}/{totalCount}";
        }
        catch
        {
            return "?/?";
        }
    }

    private async Task<string> GetLastActivityBy(GitHttpClient gitClient, Guid repositoryId, GitPullRequest pr, Guid currentUserId)
    {
        try
        {
            // Get PR threads to find the most recent activity
            var threads = await gitClient.GetThreadsAsync(repositoryId, pr.PullRequestId);
            
            if (threads?.Any() != true)
                return "Author"; // Default to author if no threads

            // Find the most recent comment/thread
            var mostRecentThread = threads
                .Where(t => t.Comments?.Any() == true)
                .SelectMany(t => t.Comments)
                .OrderByDescending(c => c.LastUpdatedDate)
                .FirstOrDefault();

            if (mostRecentThread?.Author?.Id == null)
                return "Author"; // Default to author

            var authorId = mostRecentThread.Author.Id;
            
            // Check if it's the current user
            if (authorId.ToString() == currentUserId.ToString())
                return "Me";

            // Check if it's the PR author
            if (authorId == pr.CreatedBy.Id)
                return "Author";

            // Check if it's a reviewer
            var isReviewer = pr.Reviewers?.Any(r => r.Id == authorId) == true;
            if (isReviewer)
                return "Reviewer";

            return "Other";
        }
        catch
        {
            return "Unknown";
        }
    }
}
