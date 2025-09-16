using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace gapir.Services;

/// <summary>
/// Service for managing Baffino preferences via Microsoft Graph API
/// </summary>
public class BaffinoPreferencesService
{
    private const string ExtensionId = "microsoft.teams.baffino";
    private readonly GraphAuthenticationService _authService;

    public BaffinoPreferencesService(GraphAuthenticationService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Gets the current Baffino preferences for the authenticated user
    /// </summary>
    /// <returns>BaffinoPreferences object containing current settings</returns>
    public async Task<BaffinoPreferences?> GetPreferencesAsync()
    {
        try
        {
            Log.Information("Retrieving Baffino preferences...");
            var graphClient = await _authService.GetGraphClientAsync();

            // Get the extension data for the authenticated user
            var extension = await graphClient.Me.Extensions[ExtensionId]
                .GetAsync();

            if (extension?.AdditionalData != null)
            {
                var preferences = new BaffinoPreferences();
                
                if (extension.AdditionalData.TryGetValue("timeAllocation", out var timeAllocation))
                {
                    preferences.TimeAllocation = Convert.ToInt32(timeAllocation);
                }

                if (extension.AdditionalData.TryGetValue("secondaryOnCallStrategy", out var strategy))
                {
                    preferences.SecondaryOnCallStrategy = strategy?.ToString() ?? "";
                }

                if (extension.AdditionalData.TryGetValue("privateNotifications", out var notifications))
                {
                    if (notifications is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        preferences.PrivateNotifications = jsonElement.EnumerateArray()
                            .Select(x => x.GetString() ?? "")
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();
                    }
                }

                if (extension.AdditionalData.TryGetValue("onCallSkip", out var skip))
                {
                    if (skip is System.Text.Json.JsonElement skipElement && skipElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        preferences.OnCallSkip = skipElement.EnumerateArray()
                            .Select(x => x.GetString() ?? "")
                            .ToList();
                    }
                }

                Log.Success("Successfully retrieved Baffino preferences");
                return preferences;
            }

            Log.Warning("No Baffino preferences found for the current user");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error retrieving Baffino preferences: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Updates the Baffino preferences for the authenticated user
    /// </summary>
    /// <param name="preferences">Updated preferences to set</param>
    public async Task UpdatePreferencesAsync(BaffinoPreferences preferences)
    {
        try
        {
            Log.Information("Updating Baffino preferences...");
            var graphClient = await _authService.GetGraphClientAsync();

            var extensionData = new Dictionary<string, object>
            {
                ["id"] = ExtensionId,
                ["timeAllocation"] = preferences.TimeAllocation,
                ["secondaryOnCallStrategy"] = preferences.SecondaryOnCallStrategy,
                ["privateNotifications"] = preferences.PrivateNotifications,
                ["onCallSkip"] = preferences.OnCallSkip
            };

            var extension = new Extension
            {
                AdditionalData = extensionData
            };

            await graphClient.Me.Extensions[ExtensionId]
                .PatchAsync(extension);

            Log.Success("Successfully updated Baffino preferences");
        }
        catch (Exception ex)
        {
            Log.Error($"Error updating Baffino preferences: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Updates only the time allocation setting
    /// </summary>
    /// <param name="timeAllocation">New time allocation value</param>
    public async Task UpdateTimeAllocationAsync(int timeAllocation)
    {
        var currentPreferences = await GetPreferencesAsync();
        if (currentPreferences == null)
        {
            // Create default preferences if none exist
            currentPreferences = new BaffinoPreferences
            {
                TimeAllocation = timeAllocation,
                SecondaryOnCallStrategy = "available",
                PrivateNotifications = new List<string> { "reviewAssignment", "voteReset" },
                OnCallSkip = new List<string> { "" }
            };
        }
        else
        {
            currentPreferences.TimeAllocation = timeAllocation;
        }

        await UpdatePreferencesAsync(currentPreferences);
    }
}

/// <summary>
/// Represents Baffino preferences structure
/// </summary>
public class BaffinoPreferences
{
    public int TimeAllocation { get; set; }
    public string SecondaryOnCallStrategy { get; set; } = "available";
    public List<string> PrivateNotifications { get; set; } = new();
    public List<string> OnCallSkip { get; set; } = new();
}