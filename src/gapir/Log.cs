namespace gapir;

/// <summary>
/// Lightweight static console logger with conditional output based on verbosity level.
/// Uses efficient short-circuiting to minimize overhead when verbose mode is disabled.
/// </summary>
public static class Log
{
    /// <summary>
    /// Pre-calculated emoji prefixes to avoid repeated conditional checks during logging.
    /// </summary>
    private readonly struct EmojiPrefixes
    {
        public readonly string Information;
        public readonly string Warning;
        public readonly string Error;
        public readonly string Success;
        public readonly string Debug;

        public EmojiPrefixes(bool useEmoji)
        {
            Information = useEmoji ? "ℹ️ " : "";
            Warning = useEmoji ? "⚠️ " : "";
            Error = useEmoji ? "❌" : "";
            Success = useEmoji ? "✅" : "";
            Debug = useEmoji ? "🔍" : "[DEBUG]";
        }
    }

    private static bool _isVerbose = false;
    private static EmojiPrefixes _emojis;

    /// <summary>
    /// Gets whether verbose logging is currently enabled.
    /// </summary>
    public static bool IsVerbose => _isVerbose;

    /// <summary>
    /// Initialize the logger with the specified verbosity level.
    /// Must be called before using any logging methods.
    /// </summary>
    /// <param name="verbose">Whether verbose logging is enabled</param>
    public static void Initialize(bool verbose)
    {
        _isVerbose = verbose;
        
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
        
        var useEmoji = DetectEmojiSupport();
        _emojis = new EmojiPrefixes(useEmoji);
    }

    /// <summary>
    /// Gets the appropriate output stream for logging.
    /// All logging should go to stderr to keep stdout clean for actual program output.
    /// </summary>
    private static TextWriter OutputStream { get; } =  Console.Error;

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

        OutputStream.WriteLine($"{_emojis.Information}{message}");
    }

    /// <summary>
    /// Log a warning message.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        OutputStream.WriteLine($"{_emojis.Warning}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log an error message.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        OutputStream.WriteLine($"{_emojis.Error}{message}");
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

        Console.ForegroundColor = ConsoleColor.Green;
        OutputStream.WriteLine($"{_emojis.Success}{message}");
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

        Console.ForegroundColor = ConsoleColor.Gray;
        OutputStream.WriteLine($"{_emojis.Debug} {message}");
        Console.ResetColor();
    }
}
