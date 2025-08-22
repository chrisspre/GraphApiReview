namespace gapir;

using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;
using gapir.Utilities;
using gapir.Services;
using gapir.Models;

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

        // Authentication phase - only show spinner in verbose mode
        if (Log.IsVerbose)
        {
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
        }
        else
        {
            connection = await ConsoleAuth.AuthenticateAsync(OrganizationUrl);

            if (connection == null)
            {
                Console.WriteLine("Authentication failed");
                return;
            }
        }

        if (Log.IsVerbose)
        {
            Log.Information("Successfully authenticated!");
        }

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
            // Get repository information first
            var gitClient = connection.GetClient<GitHttpClient>();
            var repository = await gitClient.GetRepositoryAsync(ProjectName, RepositoryName);

            // For now, create AzureDevOpsService without the personal access token
            // since we already have an authenticated connection
            var (currentUserId, currentUserDisplayName) = GetCurrentUserInfoAsync(connection);
            Log.Information($"Checking pull requests for user: {currentUserDisplayName}");

            // Pre-load API reviewers group members
            HashSet<string> apiReviewersMembers;
            if (Log.IsVerbose)
            {
                using (var groupSpinner = new Spinner("Loading API reviewers group..."))
                {
                    apiReviewersMembers = await GetApiReviewersGroupMembersAsync(connection);
                    groupSpinner.Success($"Loaded {apiReviewersMembers.Count} API reviewers");
                }
            }
            else
            {
                apiReviewersMembers = await GetApiReviewersGroupMembersAsync(connection);
            }

            // Initialize analysis and display services
            var analysisService = new PullRequestAnalysisService(
                apiReviewersMembers, 
                currentUserId, 
                currentUserDisplayName, 
                _options.UseShortUrls);
            
            var displayService = new PullRequestDisplayService(_options.UseShortUrls, _options.ShowDetailedTiming);

            // Get pull requests assigned to the current user
            List<GitPullRequest> pullRequests;
            if (Log.IsVerbose)
            {
                using (var prSpinner = new Spinner("Fetching pull requests..."))
                {
                    var searchCriteria = new GitPullRequestSearchCriteria()
                    {
                        Status = PullRequestStatus.Active,
                        ReviewerId = currentUserId
                    };

                    pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
                    prSpinner.Success($"Found {pullRequests.Count} assigned pull requests");
                }
            }
            else
            {
                var searchCriteria = new GitPullRequestSearchCriteria()
                {
                    Status = PullRequestStatus.Active,
                    ReviewerId = currentUserId
                };

                pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
            }

            // Analyze all pull requests
            var pullRequestInfos = await analysisService.AnalyzePullRequestsAsync(
                pullRequests, 
                gitClient, 
                repository.Id);

            // Separate into approved and pending
            var approvedPRs = pullRequestInfos.Where(info => info.IsApprovedByMe).ToList();
            var pendingPRs = pullRequestInfos.Where(info => !info.IsApprovedByMe).ToList();

            Console.WriteLine(); // Empty line before results

            // Show approved PRs if requested
            if (_options.ShowApproved && approvedPRs.Any())
            {
                Console.WriteLine($"✓ {approvedPRs.Count} PR(s) you have already approved:");
                displayService.DisplayApprovedPullRequestsTable(approvedPRs);
            }

            // Show pending PRs
            Console.WriteLine($"⏳ {pendingPRs.Count} PR(s) pending your approval:");
            if (pendingPRs.Any())
            {
                displayService.DisplayPullRequestsTable(pendingPRs);
            }
            else
            {
                Console.WriteLine("No pull requests found pending your approval.");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error occurred while checking pull requests: {ex.Message}");
            throw;
        }
    }

    private (Guid UserId, string DisplayName) GetCurrentUserInfoAsync(VssConnection connection)
    {
        try
        {
            var currentUser = connection.AuthorizedIdentity;
            return (currentUser.Id, currentUser.DisplayName);
        }
        catch
        {
            return (Guid.Empty, "Unknown User");
        }
    }
}
