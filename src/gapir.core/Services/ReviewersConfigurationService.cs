using System.Text.Json;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Service for managing the API reviewers configuration stored in JSON format
/// </summary>
public class ReviewersConfigurationService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReviewersConfigurationService()
    {
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gapir"
        );
        _configFilePath = Path.Combine(_configDirectory, "reviewers.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Load the reviewers configuration from JSON file, with fallback to hardcoded list
    /// </summary>
    public async Task<ReviewersConfiguration> LoadConfigurationAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonSerializer.Deserialize<ReviewersConfiguration>(json, _jsonOptions);
                if (config != null)
                {
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't fail - fall back to hardcoded list
            Console.WriteLine($"Warning: Could not load reviewers configuration: {ex.Message}");
        }

        // Return fallback configuration based on the current hardcoded list
        return GetFallbackConfiguration();
    }

    /// <summary>
    /// Save the reviewers configuration to JSON file
    /// </summary>
    public async Task SaveConfigurationAsync(ReviewersConfiguration configuration)
    {
        try
        {
            // Ensure the configuration directory exists
            Directory.CreateDirectory(_configDirectory);

            // Update the last updated timestamp
            configuration.LastUpdated = DateTime.UtcNow;

            // Serialize and save
            var json = JsonSerializer.Serialize(configuration, _jsonOptions);
            await File.WriteAllTextAsync(_configFilePath, json);
            
            Console.WriteLine($"✅ Reviewers configuration saved to: {_configFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving reviewers configuration: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Get the path to the configuration file
    /// </summary>
    public string GetConfigurationFilePath() => _configFilePath;

    /// <summary>
    /// Check if the configuration file exists
    /// </summary>
    public bool ConfigurationFileExists() => File.Exists(_configFilePath);

    /// <summary>
    /// Get a fallback configuration based on the current hardcoded reviewers list
    /// This maintains backward compatibility
    /// </summary>
    private static ReviewersConfiguration GetFallbackConfiguration()
    {
        return new ReviewersConfiguration
        {
            LastUpdated = DateTime.UtcNow,
            Source = "Hardcoded fallback list from GeneratedReviewers class",
            Reviewers = new List<ApiReviewer>
            {
                new() { Email = "chrispre@microsoft.com", DisplayName = "Christof Sprenger", PullRequestCount = 31 },
                new() { Email = "yadavgaurav@microsoft.com", DisplayName = "Gaurav Yadav", PullRequestCount = 25 },
                new() { Email = "kasrivat@microsoft.com", DisplayName = "Karthik Srivatsa", PullRequestCount = 24 },
                new() { Email = "duchau@microsoft.com", DisplayName = "Dulcinea Chau", PullRequestCount = 23 },
                new() { Email = "tcleveland@microsoft.com", DisplayName = "Tyler Cleveland", PullRequestCount = 18 },
                new() { Email = "shasa@microsoft.com", DisplayName = "Shantanu Saraswat", PullRequestCount = 17 },
                new() { Email = "chetanpatel@microsoft.com", DisplayName = "Chetan Patel (AAD)", PullRequestCount = 6 },
                new() { Email = "vchianese@microsoft.com", DisplayName = "Vincenzo Chianese", PullRequestCount = 4 },
                new() { Email = "jaimb@microsoft.com", DisplayName = "Jaiprakash Bankolli Mallikarjun", PullRequestCount = 2 },
                new() { Email = "etbasser@microsoft.com", DisplayName = "Etan Basseri", PullRequestCount = 1 },
                new() { Email = "dbutoyi@microsoft.com", DisplayName = "Derrick Butoyi", PullRequestCount = 1 },
                new() { Email = "dawambug@microsoft.com", DisplayName = "David Wambugu", PullRequestCount = 1 },
                new() { Email = "hut@microsoft.com", DisplayName = "Hua Tang (she her)", PullRequestCount = 1 },
                new() { Email = "eketo@microsoft.com", DisplayName = "Eric Keto", PullRequestCount = 1 },
                new() { Email = "abigailstein@microsoft.com", DisplayName = "Abigail Stein", PullRequestCount = 1 },
                new() { Email = "davidra@microsoft.com", DisplayName = "Dave Randall", PullRequestCount = 1 },
                new() { Email = "adbhale@microsoft.com", DisplayName = "Aditya Mukund", PullRequestCount = 1 },
                new() { Email = "garethj@microsoft.com", DisplayName = "Gareth Jones", PullRequestCount = 1 }
            }
        };
    }
}