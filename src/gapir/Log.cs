namespace gapir;

/// <summary>
/// Lightweight static console logger with conditional output based on verbosity level.
/// Uses efficient short-circuiting to minimize overhead when verbose mode is disabled.
/// </summary>
public static class Log
{
    private static bool _isVerbose = false;
    private static bool _useEmoji = true;
    private static bool _jsonMode = false;

    // Emoji constants - easy to switch back to colorful ones if needed
    // To use colorful emojis, change these values:
    private const string SUCCESS_EMOJI = "✅";   // Green check mark
    private const string ERROR_EMOJI = "❌";     // Red X mark  
    private const string WARNING_EMOJI = "⚠️";  // Yellow warning triangle
    // private const string SUCCESS_EMOJI = "✓";      // Simple check mark
    // private const string ERROR_EMOJI = "✗";        // Simple X mark
    // private const string WARNING_EMOJI = "!";      // Simple exclamation mark
    
    private const string SUCCESS_TEXT = "[SUCCESS]";
    private const string ERROR_TEXT = "[ERROR]";
    private const string WARNING_TEXT = "[WARNING]";

    /// <summary>
    /// Gets whether verbose logging is currently enabled.
    /// </summary>
    public static bool IsVerbose => _isVerbose;

    /// <summary>
    /// Initialize the logger with the specified verbosity level and output mode.
    /// Must be called before using any logging methods.
    /// </summary>
    /// <param name="verbose">Whether verbose logging is enabled</param>
    /// <param name="jsonMode">Whether JSON mode is enabled (logs to stderr instead of stdout)</param>
    public static void Initialize(bool verbose, bool jsonMode = false)
    {
        _isVerbose = verbose;
        _jsonMode = jsonMode;
        
        // Ensure console uses UTF-8 encoding for emoji support
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // Ignore encoding errors, fallback to no emojis
        }
        
        _useEmoji = DetectEmojiSupport();
    }

    /// <summary>
    /// Gets the appropriate output stream based on JSON mode.
    /// In JSON mode, all logging goes to stderr to keep stdout clean for JSON output.
    /// </summary>
    private static TextWriter GetOutputStream() => _jsonMode ? Console.Error : Console.Out;

    /// <summary>
    /// Detects if the current terminal supports emoji display.
    /// </summary>
    private static bool DetectEmojiSupport()
    {
        // Check if we're in VS Code terminal (generally has good emoji support)
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        if (termProgram == "vscode")
        {
            return true;
        }

        // Check for Windows Terminal
        var wtSession = Environment.GetEnvironmentVariable("WT_SESSION");
        if (!string.IsNullOrEmpty(wtSession))
        {
            return true; // Windows Terminal should support emojis with proper font
        }

        // Check for modern Windows 10/11 console
        var osVersion = Environment.OSVersion;
        if (osVersion.Platform == PlatformID.Win32NT && osVersion.Version.Major >= 10)
        {
            return true; // Modern Windows should support emojis
        }

        // For other terminals, disable emojis by default to avoid display issues
        return false;
    }

    /// <summary>
    /// Log an informational message. Only shown when verbose mode is enabled.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Information(string message)
    {
        if (!_isVerbose)
        {
            return;
        }

        var emoji = _useEmoji ? "ℹ️ " : "";
        GetOutputStream().WriteLine($"{emoji}{message}");
    }

    /// <summary>
    /// Log a warning message.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Warning(string message)
    {
        var emoji = _useEmoji ? "⚠️ " : "";
        Console.ForegroundColor = ConsoleColor.Yellow;
        GetOutputStream().WriteLine($"{emoji}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log an error message.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Error(string message)
    {
        var emoji = _useEmoji ? "❌" : "";
        Console.ForegroundColor = ConsoleColor.Red;
        GetOutputStream().WriteLine($"{emoji}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log a success message. Only shown when verbose mode is enabled.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Success(string message)
    {
        if (!_isVerbose)
        {
            return;
        }

        var emoji = _useEmoji ? "✅" : "";
        Console.ForegroundColor = ConsoleColor.Green;
        GetOutputStream().WriteLine($"{emoji}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log a debug message. Only shown when verbose mode is enabled.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Debug(string message)
    {
        if (!_isVerbose)
        {
            return;
        }

        var emoji = _useEmoji ? "🔍" : "[DEBUG]";
        Console.ForegroundColor = ConsoleColor.Gray;
        GetOutputStream().WriteLine($"{emoji} {message}");
        Console.ResetColor();
    }
}
