using Microsoft.VisualStudio.Services.WebApi;
using gapir;

namespace gapir.Services;

/// <summary>
/// Service that provides authenticated connection to Azure DevOps
/// </summary>
public class ConnectionService
{
    private readonly ConsoleAuth _consoleAuth;
    private VssConnection? _connection;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public ConnectionService(ConsoleAuth consoleAuth)
    {
        _consoleAuth = consoleAuth;
    }

    /// <summary>
    /// Gets the authenticated VssConnection
    /// </summary>
    public async Task<VssConnection?> GetConnectionAsync()
    {
        if (_connection != null)
            return _connection;

        await _semaphore.WaitAsync();
        try
        {
            if (_connection == null)
            {
                _connection = await _consoleAuth.AuthenticateAsync();
            }
            return _connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _semaphore.Dispose();
    }
}
