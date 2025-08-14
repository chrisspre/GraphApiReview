namespace gapir;

public class Spinner : IDisposable
{
    private static readonly string[] SpinnerFrames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
    private readonly Timer _timer;
    private int _currentFrame;
    private readonly string _message;
    private bool _disposed;

    public Spinner(string message)
    {
        _message = message;
        Console.Write($"{SpinnerFrames[0]} {_message}");
        Console.CursorVisible = false;
        
        _timer = new Timer(UpdateSpinner, null, TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(80));
    }

    private void UpdateSpinner(object? state)
    {
        if (_disposed) return;

        _currentFrame = (_currentFrame + 1) % SpinnerFrames.Length;
        
        // Move cursor to beginning of line and overwrite
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write($"{SpinnerFrames[_currentFrame]} {_message}");
    }

    public void Success(string? successMessage = null)
    {
        Complete("✓", successMessage);
    }

    public void Error(string? errorMessage = null)
    {
        Complete("✗", errorMessage);
    }

    private void Complete(string symbol, string? message = null)
    {
        if (_disposed) return;

        _timer?.Dispose();
        
        // Clear the line and write final message
        Console.SetCursorPosition(0, Console.CursorTop);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Console.CursorTop);
        
        if (!string.IsNullOrEmpty(message))
        {
            Console.WriteLine($"{symbol} {message}");
        }
        else
        {
            Console.WriteLine($"{symbol} {_message}");
        }
        
        Console.CursorVisible = true;
        _disposed = true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Complete("✓");
        }
    }
}
