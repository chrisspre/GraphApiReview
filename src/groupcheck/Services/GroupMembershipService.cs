using Microsoft.Graph;
using Microsoft.Graph.Models;
using gapir.Services;

namespace groupcheck.Services;

/// <summary>
/// Service for checking Azure AD group membership using Microsoft Graph API
/// </summary>
public class GroupMembershipService
{
    private readonly GroupAuthenticationService _authService;
    private readonly ConsoleLogger _logger;

    public GroupMembershipService(GroupAuthenticationService authService, ConsoleLogger logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a user is a member of a specific group
    /// </summary>
    /// <param name="userPrincipalName">User's UPN or email</param>
    /// <param name="groupNameOrId">Group name or ID</param>
    /// <returns>True if user is a member, false otherwise</returns>
    public async Task<bool> IsUserMemberOfGroupAsync(string userPrincipalName, string groupNameOrId)
    {
        try
        {
            var graphClient = await _authService.GetGraphClientAsync();
            
            // First, resolve the group ID if a name was provided
            var groupId = await ResolveGroupIdAsync(graphClient, groupNameOrId);
            if (string.IsNullOrEmpty(groupId))
            {
                _logger.Warning($"Could not find group: {groupNameOrId}");
                return false;
            }

            // Get the user ID
            var userId = await ResolveUserIdAsync(graphClient, userPrincipalName);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Warning($"Could not find user: {userPrincipalName}");
                return false;
            }

            // Check if user is a member of the group
            var memberIds = new List<string> { userId };
            var checkMemberGroupsResponse = await graphClient.DirectoryObjects[groupId].CheckMemberGroups
                .PostAsCheckMemberGroupsPostResponseAsync(new Microsoft.Graph.DirectoryObjects.Item.CheckMemberGroups.CheckMemberGroupsPostRequestBody
                {
                    GroupIds = memberIds
                });

            return checkMemberGroupsResponse?.Value?.Contains(groupId) == true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error checking group membership: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets all groups that a user is a member of
    /// </summary>
    /// <param name="userPrincipalName">User's UPN or email</param>
    /// <returns>List of groups the user belongs to</returns>
    public async Task<List<Group>> GetUserGroupsAsync(string userPrincipalName)
    {
        try
        {
            var graphClient = await _authService.GetGraphClientAsync();
            
            // Get user groups with transitive membership
            var memberOfResponse = await graphClient.Users[userPrincipalName].TransitiveMemberOf
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = "groupTypes/any(c:c eq 'Unified') or groupTypes/any(c:c eq 'DynamicMembership') or not groupTypes/any()";
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "description", "groupTypes" };
                    requestConfiguration.QueryParameters.Top = 999;
                });

            var groups = new List<Group>();
            
            if (memberOfResponse?.Value != null)
            {
                foreach (var directoryObject in memberOfResponse.Value)
                {
                    if (directoryObject is Group group)
                    {
                        groups.Add(group);
                    }
                }
            }

            return groups;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error getting user groups: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Resolves a group name to its ID, or returns the ID if already an ID
    /// </summary>
    private async Task<string?> ResolveGroupIdAsync(GraphServiceClient graphClient, string groupNameOrId)
    {
        try
        {
            // If it looks like a GUID, assume it's already an ID
            if (Guid.TryParse(groupNameOrId, out _))
            {
                return groupNameOrId;
            }

            // Search for group by display name
            var groupsResponse = await graphClient.Groups
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = $"displayName eq '{groupNameOrId}'";
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName" };
                });

            var group = groupsResponse?.Value?.FirstOrDefault();
            if (group != null)
            {
                return group.Id;
            }

            // If exact match failed, try case-insensitive search
            var allGroupsResponse = await graphClient.Groups
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter = $"startsWith(displayName, '{groupNameOrId}')";
                    requestConfiguration.QueryParameters.Select = new[] { "id", "displayName" };
                    requestConfiguration.QueryParameters.Top = 10;
                });

            var matchingGroup = allGroupsResponse?.Value?
                .FirstOrDefault(g => string.Equals(g.DisplayName, groupNameOrId, StringComparison.OrdinalIgnoreCase));

            return matchingGroup?.Id;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error resolving group ID for '{groupNameOrId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves a user UPN to its ID, or returns the ID if already an ID
    /// </summary>
    private async Task<string?> ResolveUserIdAsync(GraphServiceClient graphClient, string userPrincipalNameOrId)
    {
        try
        {
            // If it looks like a GUID, assume it's already an ID
            if (Guid.TryParse(userPrincipalNameOrId, out _))
            {
                return userPrincipalNameOrId;
            }

            // Get user by UPN
            var user = await graphClient.Users[userPrincipalNameOrId]
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "userPrincipalName", "displayName" };
                });

            return user?.Id;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error resolving user ID for '{userPrincipalNameOrId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Recursively expands group membership to find all user members
    /// </summary>
    /// <param name="groupNameOrId">Group name or ID to expand</param>
    /// <returns>List of all users who are members of the group (directly or through nested groups)</returns>
    public async Task<List<User>> GetAllUsersInGroupRecursiveAsync(string groupNameOrId)
    {
        try
        {
            var graphClient = await _authService.GetGraphClientAsync();
            
            // First, resolve the group ID if a name was provided
            var groupId = await ResolveGroupIdAsync(graphClient, groupNameOrId);
            if (string.IsNullOrEmpty(groupId))
            {
                _logger.Warning($"Could not find group: {groupNameOrId}");
                return new List<User>();
            }

            var allUsers = new HashSet<string>(); // Use HashSet to avoid duplicates
            var processedGroups = new HashSet<string>(); // Track processed groups to avoid infinite loops
            
            await ExpandGroupMembershipRecursive(graphClient, groupId, allUsers, processedGroups);
            
            // Convert user IDs to User objects
            var users = new List<User>();
            foreach (var userId in allUsers)
            {
                try
                {
                    var user = await graphClient.Users[userId]
                        .GetAsync(requestConfiguration =>
                        {
                            requestConfiguration.QueryParameters.Select = new[] { "id", "displayName", "userPrincipalName" };
                        });
                    
                    if (user != null)
                    {
                        users.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Could not retrieve user {userId}: {ex.Message}");
                }
            }
            
            return users;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error expanding group membership: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Recursively expands a group to find all user members
    /// </summary>
    private async Task ExpandGroupMembershipRecursive(
        GraphServiceClient graphClient, 
        string groupId, 
        HashSet<string> allUsers, 
        HashSet<string> processedGroups)
    {
        // Avoid infinite loops by checking if we've already processed this group
        if (processedGroups.Contains(groupId))
        {
            return;
        }
        
        processedGroups.Add(groupId);
        _logger.Information($"Expanding group: {groupId}");

        try
        {
            // Get all members of the current group
            var membersResponse = await graphClient.Groups[groupId].Members
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id", "@odata.type" };
                    requestConfiguration.QueryParameters.Top = 999;
                });

            if (membersResponse?.Value != null)
            {
                foreach (var member in membersResponse.Value)
                {
                    // Check the type of the member
                    var odataType = member.OdataType;
                    
                    if (odataType == "#microsoft.graph.user")
                    {
                        // It's a user, add to our collection
                        if (!string.IsNullOrEmpty(member.Id))
                        {
                            allUsers.Add(member.Id);
                        }
                    }
                    else if (odataType == "#microsoft.graph.group")
                    {
                        // It's a nested group, recursively expand it
                        if (!string.IsNullOrEmpty(member.Id))
                        {
                            await ExpandGroupMembershipRecursive(graphClient, member.Id, allUsers, processedGroups);
                        }
                    }
                    // Ignore other types (devices, service principals, etc.)
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"Error expanding group {groupId}: {ex.Message}");
        }
    }
}
