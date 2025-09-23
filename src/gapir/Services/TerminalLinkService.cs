namespace gapir.Services;

using gapir.Models;

/// <summary>
/// Service for generating OSC 8 hyperlinks in terminal output
/// OSC 8 allows terminals to display clickable links without external URL shortening services
/// </summary>
public class TerminalLinkService
{
    private readonly AzureDevOpsConfiguration _adoConfig;

    public TerminalLinkService(AzureDevOpsConfiguration adoConfig)
    {
        _adoConfig = adoConfig;
    }
    /// <summary>
    /// Creates a clickable link in the terminal using OSC 8 escape sequences
    /// Falls back to displaying just the text in terminals that don't support OSC 8
    /// </summary>
    /// <param name="text">The text to display</param>
    /// <param name="url">The URL to link to</param>
    /// <returns>OSC 8 formatted string for terminal display</returns>
    public static string CreateLink(string text, string url)
    {
        // OSC 8 format: \e]8;;{url}\e\\{text}\e]8;;\e\\
        // This creates a clickable link in supported terminals
        // In unsupported terminals, only the text will be displayed
        return $"\u001b]8;;{url}\u001b\\{text}\u001b]8;;\u001b\\";
    }

    /// <summary>
    /// Creates a clickable link for a pull request using its ID and title
    /// </summary>
    /// <param name="pullRequestId">The pull request ID</param>
    /// <param name="title">The pull request title</param>
    /// <returns>OSC 8 formatted clickable link</returns>
    public string CreatePullRequestLink(int pullRequestId, string title)
    {
        var url = _adoConfig.GetPullRequestUrl(pullRequestId);
        return CreateLink(title, url);
    }

    /// <summary>
    /// Checks if the current terminal likely supports OSC 8 links
    /// This is a best-effort detection and may not be 100% accurate
    /// </summary>
    /// <returns>True if terminal likely supports OSC 8</returns>
    public static bool SupportsOsc8()
    {
        // Check for known terminals that support OSC 8
        var terminalProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var term = Environment.GetEnvironmentVariable("TERM");
        
        // Windows Terminal, VS Code terminal, and many modern terminals support OSC 8
        return terminalProgram switch
        {
            "vscode" => true,           // VS Code integrated terminal
            "Windows Terminal" => true, // Windows Terminal
            "iTerm.app" => true,        // iTerm2 on macOS
            _ => term?.Contains("xterm") == true || term?.Contains("screen") == true
        };
    }
}