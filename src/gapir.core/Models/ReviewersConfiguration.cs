using System.Text.Json.Serialization;

namespace gapir.Models;

/// <summary>
/// Configuration model for API reviewers list
/// </summary>
public class ReviewersConfiguration
{
    /// <summary>
    /// The date/time when this configuration was last updated
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source information about how this list was generated
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "Generated from Azure DevOps API review pull requests";

    /// <summary>
    /// List of known API reviewers with their email addresses and metadata
    /// </summary>
    [JsonPropertyName("reviewers")]
    public List<ApiReviewer> Reviewers { get; set; } = new();

    /// <summary>
    /// Get just the email addresses as a HashSet for quick lookups
    /// </summary>
    public HashSet<string> GetEmailAddresses()
    {
        return new HashSet<string>(
            Reviewers.Select(r => r.Email), 
            StringComparer.OrdinalIgnoreCase
        );
    }
}

/// <summary>
/// Individual API reviewer information
/// </summary>
public class ApiReviewer
{
    /// <summary>
    /// Email address of the reviewer
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the reviewer
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Number of pull requests this reviewer has been involved in (for statistics)
    /// </summary>
    [JsonPropertyName("pullRequestCount")]
    public int PullRequestCount { get; set; }

    /// <summary>
    /// When this reviewer was last seen in the system
    /// </summary>
    [JsonPropertyName("lastSeen")]
    public DateTime? LastSeen { get; set; }
}