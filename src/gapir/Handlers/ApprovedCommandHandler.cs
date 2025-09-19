using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the approved command - shows pull requests already approved by the user
/// </summary>
public class ApprovedCommandHandler
{
    private readonly ApprovedPullRequestService _pullRequestService;
    private readonly PullRequestRenderingService _renderer;
    private readonly ConnectionService _connectionService;
    private readonly ConsoleLogger _logger;

    public ApprovedCommandHandler(
        ApprovedPullRequestService pullRequestService,
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
    /// Handles the approved command execution
    /// </summary>
    public async Task<int> HandleAsync(ApprovedOptions options, GlobalOptions globalOptions)
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
            var result = await _pullRequestService.GetApprovedPullRequestsAsync(connection);
            
            var renderingOptions = new PullRequestRenderingOptions
            {
                ShowDetailedTiming = options.DetailedTiming,
                ShowDetailedInfo = options.ShowDetailedInfo,
                Format = globalOptions.Format
            };

            _renderer.RenderApprovedPullRequests(result, renderingOptions);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing approved command: {ex.Message}");
            return 1;
        }
    }
}
