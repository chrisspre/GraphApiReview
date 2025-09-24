namespace gapir.Services;

/// <summary>
/// Console-based logger that writes to stderr.
/// Uses efficient short-circuiting to minimize overhead when verbose mode is disabled.
/// </summary>
public class ConsoleLogger
{
    /// <summary>
    /// Pre-calculated log level prefixes to avoid repeated conditional checks during logging.
    /// Supports both emoji and text-based prefixes with consistent spacing.
    /// </summary>
    private readonly struct LogLevelPrefixes
    {
        public readonly string Information;
        public readonly string Warning;
        public readonly string Error;
        public readonly string Success;
        public readonly string Debug;

        public LogLevelPrefixes(bool useEmoji)
        {
            Information = useEmoji ? "ℹ️ " : "[INFO] ";
            Warning = useEmoji ? "⚠️ " : "[WARN] ";
            Error = useEmoji ? "❌ " : "[ERROR] ";
            Success = useEmoji ? "✅ " : "[SUCCESS] ";
            Debug = useEmoji ? "🔍 " : "[DEBUG] ";
        }
    }

    private bool _isVerbose;
    private LogLevelPrefixes _prefixes;
    private readonly TextWriter _outputStream;

    /// <summary>
    /// Initializes a new instance of the ConsoleLogger.
    /// Call SetVerbosity() to configure verbosity level.
    /// </summary>
    public ConsoleLogger()
    {
        _outputStream = Console.Error; // All logging goes to stderr to keep stdout clean
        
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
        
        // Initialize with default verbosity (will be set properly by SetVerbosity)
        SetVerbosity(false);
    }

    /// <summary>
    /// Sets the verbosity level for this logger instance.
    /// </summary>
    /// <param name="verbose">Whether verbose logging is enabled</param>
    public void SetVerbosity(bool verbose)
    {
        _isVerbose = verbose;
        
        // Initialize the static Log class for gapir.core to use the same verbosity
        if (!Log.IsInitialized)
        {
            Log.Initialize(verbose);
        }
        
        var useEmoji = DetectEmojiSupport();
        _prefixes = new LogLevelPrefixes(useEmoji);
    }

    /// <summary>
    /// Gets whether verbose logging is currently enabled.
    /// </summary>
    public bool IsVerbose => _isVerbose;

    /// <summary>
    /// Log an informational message. Only shown when verbose mode is enabled.
    /// </summary>
    public void Information(string message)
    {
        if (!_isVerbose)
        {
            return;
        }

        _outputStream.WriteLine($"{_prefixes.Information}{message}");
    }

    /// <summary>
    /// Log a warning message. Always shown.
    /// </summary>
    public void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        _outputStream.WriteLine($"{_prefixes.Warning}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log an error message. Always shown.
    /// </summary>
    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        _outputStream.WriteLine($"{_prefixes.Error}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log a success message. Only shown when verbose mode is enabled.
    /// </summary>
    public void Success(string message)
    {
        if (!_isVerbose)
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        _outputStream.WriteLine($"{_prefixes.Success}{message}");
        Console.ResetColor();
    }

    /// <summary>
    /// Log a debug message. Only shown when verbose mode is enabled.
    /// </summary>
    public void Debug(string message)
    {
        if (!_isVerbose)
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Gray;
        _outputStream.WriteLine($"{_prefixes.Debug}{message}");
        Console.ResetColor();
    }

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
}
