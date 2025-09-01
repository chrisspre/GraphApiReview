using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the collect command - collects reviewers and generates code
/// </summary>
public class CollectCommandHandler
{
    private readonly ReviewerCollector _reviewerCollector;
    private readonly ConnectionService _connectionService;
    private readonly ConsoleLogger _logger;

    public CollectCommandHandler(
        ReviewerCollector reviewerCollector,
        ConnectionService connectionService,
        ConsoleLogger logger)
    {
        _reviewerCollector = reviewerCollector;
        _connectionService = connectionService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the collect command execution
    /// </summary>
    public async Task<int> HandleAsync(CollectOptions options, GlobalOptions globalOptions)
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
            await _reviewerCollector.CollectAndGenerateAsync(connection);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing collect command: {ex.Message}");
            return 1;
        }
    }
}
