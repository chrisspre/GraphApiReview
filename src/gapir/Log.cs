namespace gapir;

/// <summary>
/// Lightweight static console logger with conditional output based on verbosity level.
/// Uses efficient short-circuiting to minimize overhead when verbose mode is disabled.
/// </summary>
public static class Log
{
    private static bool _isVerbose = false;

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
    }

    /// <summary>
    /// Logs an informational message when verbose mode is enabled.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Information(string message)
    {
        if (_isVerbose)
        {
            Console.WriteLine(message);
        }
    }

    /// <summary>
    /// Logs a success message when verbose mode is enabled. Prepends the message with a ✅ prefix.
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Success(string message)
    {
        if (_isVerbose)
        {
            Console.WriteLine($"✅ {message}");
        }
    }

    /// <summary>
    /// Logs an error message when verbose mode is enabled. Prepends the message with a ❌ prefix.
    /// </summary>
    /// <param name="message">The error message to log</param>
    public static void Error(string message)
    {
        if (_isVerbose)
        {
            Console.WriteLine($"❌ {message}");
        }
    }

    /// <summary>
    /// Logs a warning message when verbose mode is enabled. Prepends the message with a ⚠️ prefix.
    /// </summary>
    /// <param name="message">The warning message to log</param>
    public static void Warning(string message)
    {
        if (_isVerbose)
        {
            Console.WriteLine($"⚠️  {message}");
        }
    }
}
