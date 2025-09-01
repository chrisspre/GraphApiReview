using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the diagnose command - shows diagnostic information for a specific PR
/// </summary>
public class DiagnoseCommandHandler
{
    private readonly PullRequestDiagnostics _diagnosticChecker;
    private readonly ConnectionService _connectionService;
    private readonly ConsoleLogger _logger;

    public DiagnoseCommandHandler(
        PullRequestDiagnostics diagnosticChecker,
        ConnectionService connectionService,
        ConsoleLogger logger)
    {
        _diagnosticChecker = diagnosticChecker;
        _connectionService = connectionService;
        _logger = logger;
    }

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
            var result = await _diagnosticChecker.GetDiagnosticResultAsync(options.PullRequestId);
            
            // For now, just output the result. In the future, we could add a rendering service
            Console.WriteLine($"Diagnostic result for PR #{options.PullRequestId}:");
            Console.WriteLine($"Status: {result}");
            
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing diagnose command: {ex.Message}");
            return 1;
        }
    }
}
