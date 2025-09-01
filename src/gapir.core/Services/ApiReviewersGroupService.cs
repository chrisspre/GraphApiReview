namespace gapir.Services;

using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Identity;
using Microsoft.VisualStudio.Services.Identity.Client;

public class ApiReviewersGroupService
{

    public async Task<HashSet<string>> GetGroupMembersAsync(VssConnection connection)
    {
        HashSet<string> groupMembers;

        try
        {
            var identityClient = connection.GetClient<IdentityHttpClient>();

            Log.Information($"Fetching API reviewers group: {AdoConfig.ReviewersGroupName}");

            // Step 1: Find the group by exact name
            var searchResults = await identityClient.ReadIdentitiesAsync(
                IdentitySearchFilter.General,
                AdoConfig.ReviewersGroupName,
                queryMembership: QueryMembership.None);

            var apiGroup = searchResults?.FirstOrDefault(i =>
                i.DisplayName?.Equals(AdoConfig.ReviewersGroupName, StringComparison.OrdinalIgnoreCase) == true);

            if (apiGroup != null)
            {
                Log.Information($"Found group: {apiGroup.DisplayName}, Id: {apiGroup.Id}");

                // Step 2: Get group members with recursive expansion
                groupMembers = await ExpandGroupMembersRecursively(identityClient, apiGroup.Id);

                Log.Information($"Found {groupMembers.Count} API reviewers via group membership");
            }
            else
            {
                Log.Warning($"Group '{AdoConfig.ReviewersGroupName}' not found");
                groupMembers = new HashSet<string>();
            }

            // Step 3: Use static fallback if no members found
            if (groupMembers.Count == 0)
            {
                Log.Warning("No API reviewers found via group membership, using static fallback");
                groupMembers = GetStaticApiReviewersFallback();
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching API reviewers: {ex.Message}");
            Log.Information("Using static fallback list");
            groupMembers = GetStaticApiReviewersFallback();
        }

        return groupMembers;
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

}
