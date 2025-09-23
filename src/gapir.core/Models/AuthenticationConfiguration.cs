namespace gapir.Models;

/// <summary>
/// Configuration for authentication settings
/// </summary>
public class AuthenticationConfiguration
{
    /// <summary>
    /// Azure CLI Client ID for authentication
    /// This is the standard Azure CLI client ID that can be used for interactive authentication
    /// </summary>
    public string ClientId { get; init; } = "04b07795-8ddb-461a-bbee-02f9e1bf7b46";
}