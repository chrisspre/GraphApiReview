using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the completed command - shows pull requests completed in the last 30 days where user was a reviewer
/// </summary>
public class CompletedCommandHandler
{
    private readonly CompletedPullRequestService _pullRequestService;
    private readonly PullRequestRenderingService _renderer;
    private readonly ConnectionService _connectionService;
    private readonly ConsoleLogger _logger;

    public CompletedCommandHandler(
        CompletedPullRequestService pullRequestService,
        PullRequestRenderingService renderer,
        ConnectionService connectionService,
        ConsoleLogger logger)
    {
        _pullRequestService = pullRequestService;
        _renderer = renderer;
        _connectionService = connectionService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the completed command execution
    /// </summary>
    public async Task<int> HandleAsync(CompletedOptions options, GlobalOptions globalOptions)
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
            var result = await _pullRequestService.GetCompletedPullRequestsAsync(connection, options.DaysBack);
            
            var renderingOptions = new PullRequestRenderingOptions
            {
                ShowDetailedTiming = options.DetailedTiming,
                ShowDetailedInfo = options.ShowDetailedInfo,
                Format = globalOptions.Format
            };

            _renderer.RenderCompletedPullRequests(result, renderingOptions);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing completed command: {ex.Message}");
            return 1;
        }
    }
}