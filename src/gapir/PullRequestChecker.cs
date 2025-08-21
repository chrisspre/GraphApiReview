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
    private const string ApiReviewersGroupName = "[TEAM FOUNDATION]\\Microsoft Graph API reviewers";

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

            Log.Information($"Fetching API reviewers group: {ApiReviewersGroupName}");

            // Step 1: Find the group by exact name
            var searchResults = await identityClient.ReadIdentitiesAsync(
                IdentitySearchFilter.General,
                ApiReviewersGroupName,
                queryMembership: QueryMembership.None);

            var apiGroup = searchResults?.FirstOrDefault(i =>
                i.DisplayName?.Equals(ApiReviewersGroupName, StringComparison.OrdinalIgnoreCase) == true);

            if (apiGroup != null)
            {
                Log.Information($"Found group: {apiGroup.DisplayName}, Id: {apiGroup.Id}");

                // Step 2: Get group members with recursive expansion
                _apiReviewersMembers = await ExpandGroupMembersRecursively(identityClient, apiGroup.Id);

                Log.Information($"Found {_apiReviewersMembers.Count} API reviewers via group membership");
            }
            else
            {
                Log.Warning($"Group '{ApiReviewersGroupName}' not found");
                _apiReviewersMembers = new HashSet<string>();
            }

            // Step 3: Use static fallback if no members found
            if (_apiReviewersMembers.Count == 0)
            {
                Log.Warning("No API reviewers found via group membership, using static fallback");
                _apiReviewersMembers = GetStaticApiReviewersFallback();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching API reviewers: {ex.Message}");
            Log.Information("Using static fallback list");
            _apiReviewersMembers = GetStaticApiReviewersFallback();
        }

        return _apiReviewersMembers;
    }

    private async Task<HashSet<string>> ExpandGroupMembersRecursively(IdentityHttpClient identityClient, Guid groupId)
    {
        var allMembers = new HashSet<string>();
        var processedGroups = new HashSet<Guid>();

        await ExpandGroupMembersRecursivelyInternal(identityClient, groupId, allMembers, processedGroups);

        return allMembers;
    }

    private async Task ExpandGroupMembersRecursivelyInternal(IdentityHttpClient identityClient, Guid groupId, HashSet<string> allMembers, HashSet<Guid> processedGroups)
    {
        // Avoid infinite recursion
        if (processedGroups.Contains(groupId))
            return;

        processedGroups.Add(groupId);

        try
        {
            // Try to get group with expanded membership
            var group = await identityClient.ReadIdentityAsync(groupId, QueryMembership.Expanded);

            if (group?.Members?.Any() == true)
            {
                foreach (var member in group.Members)
                {
                    // Use the identifier string to determine type
                    var identifier = member.Identifier;

                    // If the identifier looks like a GUID, it might be a nested group
                    if (Guid.TryParse(identifier, out var memberGuid))
                    {
                        try
                        {
                            // Try to read this member to see if it's a group
                            var memberIdentity = await identityClient.ReadIdentityAsync(memberGuid, QueryMembership.None);

                            // Check if this is a group by looking at the descriptor
                            if (memberIdentity?.Descriptor?.Identifier?.StartsWith("vssgp.") == true)
                            {
                                Log.Debug($"Found nested group: {memberIdentity.DisplayName}, expanding...");
                                await ExpandGroupMembersRecursivelyInternal(identityClient, memberGuid, allMembers, processedGroups);
                            }
                            else
                            {
                                // It's a user
                                allMembers.Add(identifier);
                                Log.Debug($"Added user: {memberIdentity?.DisplayName} ({identifier})");
                            }
                        }
                        catch (Exception ex)
                        {
                            // If we can't read it as an identity, treat it as a user ID
                            Log.Debug($"Treating as user ID: {identifier} ({ex.Message})");
                            allMembers.Add(identifier);
                        }
                    }
                    else
                    {
                        // Non-GUID identifier, treat as user
                        allMembers.Add(identifier);
                        Log.Debug($"Added user (non-GUID): {identifier}");
                    }
                }
            }
            // Fallback: try MemberIds if Members is empty
            else if (group?.MemberIds?.Any() == true)
            {
                Log.Debug($"Using MemberIds fallback for group {groupId}");
                foreach (var memberId in group.MemberIds)
                {
                    try
                    {
                        var member = await identityClient.ReadIdentityAsync(memberId, QueryMembership.None);
                        if (member?.Descriptor?.Identifier?.StartsWith("vssgp.") == true)
                        {
                            Log.Debug($"Found nested group via MemberIds: {member.DisplayName}, expanding...");
                            await ExpandGroupMembersRecursivelyInternal(identityClient, memberId, allMembers, processedGroups);
                        }
                        else
                        {
                            allMembers.Add(memberId.ToString());
                            Log.Debug($"Added user via MemberIds: {member?.DisplayName} ({memberId})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug($"Could not read member {memberId}: {ex.Message}");
                        // Assume it's a user if we can't read it
                        allMembers.Add(memberId.ToString());
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"Error expanding group {groupId}: {ex.Message}");
        }
    }

    private static HashSet<string> GetStaticApiReviewersFallback()
    {
        // Return static list from generated class
        Log.Information($"Using static fallback list with {ApiReviewersFallback.KnownApiReviewers.Count} known API reviewers");
        return new HashSet<string>(ApiReviewersFallback.KnownApiReviewers, StringComparer.OrdinalIgnoreCase);
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
            var pendingHeaders = new[] { "Title", "Author", "Age", "Status", "Ratio", "Change", "URL" };
            var pendingMaxWidths = new[] { 30, 18, 10, 6, 6, 20, -1 }; // -1 means no limit for URLs

            var pendingRows = new List<string[]>();
            foreach (var pr in pendingPullRequests)
            {
                var timeAssigned = GetTimeAssignedToReviewer(pr, currentUser.Id);
                var url = GetFullPullRequestUrl(pr, _options.UseShortUrls);
                var myVoteStatus = GetMyVoteStatus(pr, currentUser.Id, currentUser.DisplayName);
                var apiApprovalRatio = GetApiApprovalRatio(pr, apiReviewersMembers);
                var lastActivity = await GetLastChangeInfo(gitClient, repository.Id, pr, currentUser.Id);

                pendingRows.Add([
                    ShortenTitle(pr.Title),
                    pr.CreatedBy.DisplayName,
                    timeAssigned,
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
                _ => "Unknow" // Unknown
            };
        }
        catch
        {
            return "Error"; // Error
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

            // Identify API reviewers using group membership data
            List<IdentityRefWithVote> apiReviewers;
            List<IdentityRefWithVote> nonApiReviewers;

            // Use group membership data - convert Guid to string for comparison
            apiReviewers = allReviewers.Where(r => apiReviewersMembers.Contains(r.Id.ToString())).ToList();
            nonApiReviewers = allReviewers.Where(r =>
                !apiReviewersMembers.Contains(r.Id.ToString()) &&
                !r.DisplayName.StartsWith("[TEAM FOUNDATION]") &&
                !r.DisplayName.StartsWith($"[{ProjectName}]") &&
                !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
            ).ToList();

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

            // Filter to only API reviewers - check both email addresses and unique names
            var apiReviewers = pr.Reviewers?.Where(r => 
                apiReviewersMembers.Contains(r.UniqueName) || 
                apiReviewersMembers.Contains(r.Id.ToString())).ToList();

            if (apiReviewers == null || apiReviewers.Count == 0)
            {
                return "0/0"; // No API reviewers assigned
            }
            var approvedCount = apiReviewers.Count(r => r.Vote >= 5); // 5 = approved with suggestions, 10 = approved
            var totalCount = apiReviewers.Count;

            return $"{approvedCount}/{totalCount}";
        }
        catch
        {
            return "?/?";
        }
    }

    private async Task<string> GetLastChangeInfo(GitHttpClient gitClient, Guid repositoryId, GitPullRequest pr, Guid currentUserId)
    {
        try
        {
            DateTime? mostRecentActivityDate = null;
            string? mostRecentActivityAuthorId = null;
            string changeType = "Created";

            // Check 1: PR threads/comments - look for different types of comments
            var threads = await gitClient.GetThreadsAsync(repositoryId, pr.PullRequestId);
            if (threads?.Any() == true)
            {
                var mostRecentComment = threads
                    .Where(t => t.Comments?.Any() == true)
                    .SelectMany(t => t.Comments)
                    .OrderByDescending(c => c.LastUpdatedDate)
                    .FirstOrDefault();

                if (mostRecentComment?.LastUpdatedDate != null)
                {
                    mostRecentActivityDate = mostRecentComment.LastUpdatedDate;
                    mostRecentActivityAuthorId = mostRecentComment.Author?.Id;
                    
                    // Determine comment type based on thread properties
                    var thread = threads.FirstOrDefault(t => t.Comments?.Contains(mostRecentComment) == true);
                    if (thread?.Properties?.Any(p => p.Key == "Microsoft.TeamFoundation.Discussion.Status") == true)
                    {
                        var status = thread.Properties.FirstOrDefault(p => p.Key == "Microsoft.TeamFoundation.Discussion.Status").Value?.ToString();
                        changeType = status?.ToLower() switch
                        {
                            "active" => "Added Comment",
                            "fixed" => "Resolved Comment", 
                            "wontfix" => "Won't Fix Comment",
                            "closed" => "Closed Comment",
                            _ => "Added Comment"
                        };
                    }
                    else
                    {
                        changeType = "Added Comment";
                    }
                }
            }

            // Check 2: PR iterations (code pushes)
            try
            {
                var iterations = await gitClient.GetPullRequestIterationsAsync(repositoryId, pr.PullRequestId);
                var mostRecentIteration = iterations?.OrderByDescending(i => i.CreatedDate).FirstOrDefault();
                
                if (mostRecentIteration?.CreatedDate != null && 
                    (mostRecentActivityDate == null || mostRecentIteration.CreatedDate > mostRecentActivityDate))
                {
                    mostRecentActivityDate = mostRecentIteration.CreatedDate;
                    mostRecentActivityAuthorId = pr.CreatedBy.Id;
                    changeType = "Pushed Code";
                }
            }
            catch
            {
                // Ignore iteration errors - some permissions issues might occur
            }

            // Check 3: Reviewer votes/approvals (this will override if we detect a vote from the same person)
            if (pr.Reviewers?.Any() == true)
            {
                foreach (var reviewer in pr.Reviewers)
                {
                    if (reviewer.Vote != 0 && reviewer.Id.ToString() == mostRecentActivityAuthorId)
                    {
                        // If the most recent activity author also has a vote, show the vote instead
                        changeType = reviewer.Vote switch
                        {
                            -10 => "Rejected",
                            -5 => "Waiting for Author",
                            5 => "Approved with Suggestions",
                            10 => "Approved",
                            _ => "Voted"
                        };
                        break;
                    }
                }
            }

            // Fallback to PR creation if no activity found
            if (string.IsNullOrEmpty(mostRecentActivityAuthorId))
            {
                mostRecentActivityAuthorId = pr.CreatedBy.Id;
                changeType = "Created PR";
            }

            // Determine the relationship (who)
            string actor;
            if (mostRecentActivityAuthorId == currentUserId.ToString())
                actor = "Me";
            else if (mostRecentActivityAuthorId == pr.CreatedBy.Id)
                actor = "Author";
            else
            {
                var isReviewer = pr.Reviewers?.Any(r => r.Id.ToString() == mostRecentActivityAuthorId) == true;
                actor = isReviewer ? "Reviewer" : "Other";
            }

            // Format as "Who: What"
            return $"{actor}: {changeType}";
        }
        catch
        {
            return "Unknown";
        }
    }
}
