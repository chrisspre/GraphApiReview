using System.CommandLine;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Command handler for Baffino preferences management
/// </summary>
public class BaffinoCommandHandler
{
    private readonly BaffinoPreferencesService _preferencesService;

    public BaffinoCommandHandler(BaffinoPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
    }

    /// <summary>
    /// Handles the get baffino preferences command
    /// </summary>
    public async Task<int> HandleGetPreferencesAsync(string format, bool verbose, bool showAll)
    {
        try
        {
            if (verbose)
            {
                Log.Information("🔍 Retrieving Baffino preferences...");
            }
            
            var preferences = await _preferencesService.GetPreferencesAsync();
            
            if (preferences == null)
            {
                Console.WriteLine("❌ No Baffino preferences found for the current user.");
                Console.WriteLine("   You may need to set up Baffino preferences first using the 'baffino set' command.");
                return 1;
            }

            if (!verbose)
            {
                Console.WriteLine();
                Console.WriteLine("✅ Current Baffino Preferences:");
                Console.WriteLine();
            }

            switch (format.ToLower())
            {
                case "json":
                    var jsonOptions = new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    };
                    
                    if (showAll)
                    {
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(preferences, jsonOptions));
                    }
                    else
                    {
                        var timeAllocationOnly = new { timeAllocation = preferences.TimeAllocation };
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(timeAllocationOnly, jsonOptions));
                    }
                    break;
                
                default: // table format
                    if (showAll)
                    {
                        Console.WriteLine($"Time Allocation:       {preferences.TimeAllocation}");
                        Console.WriteLine($"Secondary On-Call:     {preferences.SecondaryOnCallStrategy}");
                        Console.WriteLine($"Private Notifications: {string.Join(", ", preferences.PrivateNotifications)}");
                        Console.WriteLine($"On-Call Skip:          {string.Join(", ", preferences.OnCallSkip.Where(x => !string.IsNullOrEmpty(x)))}");
                    }
                    else
                    {
                        Console.WriteLine($"Time Allocation: {preferences.TimeAllocation}");
                    }
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error retrieving preferences: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Handles the set baffino time allocation command
    /// </summary>
    public async Task<int> HandleSetTimeAllocationAsync(int timeAllocation, bool verbose)
    {
        try
        {
            if (timeAllocation < 0 || timeAllocation > 100)
            {
                Console.WriteLine("❌ Time allocation must be between 0 and 100.");
                return 1;
            }

            if (verbose)
            {
                Log.Information($"⚙️  Setting time allocation to {timeAllocation}...");
            }
            
            await _preferencesService.UpdateTimeAllocationAsync(timeAllocation);
            
            if (!verbose)
            {
                Console.WriteLine();
                Console.WriteLine($"✅ Successfully updated time allocation to {timeAllocation}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating preferences: {ex.Message}");
            return 1;
        }
    }
}