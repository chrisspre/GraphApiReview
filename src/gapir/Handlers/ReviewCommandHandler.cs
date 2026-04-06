using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the review command - shows pull requests pending review
/// </summary>
public class ReviewCommandHandler
{
    private readonly PendingPullRequestService _pullRequestService;
    private readonly RenderingService _renderer;
    private readonly ConnectionService _connectionService;
    private readonly ConsoleLogger _logger;

    public ReviewCommandHandler(
        PendingPullRequestService pullRequestService,
        RenderingService renderer,
        ConnectionService connectionService,
        ConsoleLogger logger)
    {
        _pullRequestService = pullRequestService;
        _renderer = renderer;
        _connectionService = connectionService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the review command execution
    /// </summary>
    public async Task<int> HandleAsync(ReviewOptions options, GlobalOptions globalOptions)
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
            var result = await _pullRequestService.GetPendingPullRequestsAsync(connection);

            if (options.NoVoteOnly)
            {
                result.PendingPullRequests = result.PendingPullRequests
                    .Where(pr => pr.MyVoteStatus == "NoVote")
                    .ToList();
            }
            
            var renderingOptions = new PullRequestRenderingOptions
            {
                ShowDetailedTiming = options.DetailedTiming,
                ShowDetailedInfo = options.ShowDetailedInfo
            };

            _renderer.RenderPendingPullRequests(result, renderingOptions);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing review command: {ex.Message}");
            return 1;
        }
    }
}
