using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the diagnose command - shows diagnostic information for a specific PR
/// </summary>
public class DiagnoseCommandHandler(
    PullRequestDiagnostics diagnosticChecker,
    ConnectionService connectionService,
    ConsoleLogger logger)
{
    private readonly PullRequestDiagnostics _diagnosticChecker = diagnosticChecker;
    private readonly ConnectionService _connectionService = connectionService;
    private readonly ConsoleLogger _logger = logger;

    /// <summary>
    /// Handles the diagnose command execution
    /// </summary>
    public async Task<int> HandleAsync(DiagnoseOptions options, GlobalOptions globalOptions)
    {
        _logger.SetVerbosity(globalOptions.Verbose);
        
        var connection = await _connectionService.GetConnectionAsync();
        if (connection == null)
        {
            _logger.Error("Unable to log in. Exiting");
            return 1;
        }

        try
        {
            var filePath = await _diagnosticChecker.GetDiagnosticResultAsync(options.PullRequestId);
            Console.WriteLine($"Diagnostic data for PR #{options.PullRequestId} saved to: {filePath}");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing diagnose command: {ex.Message}");
            return 1;
        }
    }
}
