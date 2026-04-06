using gapir.Models;
using gapir.Services;

namespace gapir.Handlers;

/// <summary>
/// Handler for the report command - shows completed PRs approved by the user, grouped by week
/// </summary>
public class ReportCommandHandler
{
    private readonly CompletedPullRequestService _pullRequestService;
    private readonly RenderingService _renderer;
    private readonly ConnectionService _connectionService;
    private readonly ConsoleLogger _logger;

    public ReportCommandHandler(
        CompletedPullRequestService pullRequestService,
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
    /// Handles the report command execution
    /// </summary>
    public async Task<int> HandleAsync(ReportOptions options, GlobalOptions globalOptions)
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
            // Compute week-aligned date range
            // "Last week" means the previous Mon 00:00 to Sun 23:59:59
            // On Monday morning you see the previous full work week + weekend
            var today = DateTime.UtcNow.Date;
            var daysFromMonday = ((int)today.DayOfWeek + 6) % 7; // Monday=0, Sunday=6
            var thisMonday = today.AddDays(-daysFromMonday);
            var rangeEnd = options.IncludeCurrentWeek
                ? today.AddDays(1) // exclusive: tomorrow 00:00 (includes current day)
                : thisMonday; // exclusive: this Monday 00:00 (i.e. Sun 23:59:59 is included)
            var rangeStart = thisMonday.AddDays(-7 * options.Weeks);

            // Fetch enough data to cover the range (add buffer for timezone differences)
            var daysBack = (int)(today - rangeStart).TotalDays + 7;
            var result = await _pullRequestService.GetCompletedPullRequestsAsync(connection, daysBack);

            // Filter to only PRs approved by the current user within the week range
            var approvedByMe = result.CompletedPullRequests
                .Where(pr => pr.IsApprovedByMe || pr.MyVoteStatus is "Apprvd" or "Sugges")
                .Where(pr => pr.PullRequest.ClosedDate >= rangeStart && pr.PullRequest.ClosedDate < rangeEnd)
                .ToList();

            var renderingOptions = new PullRequestRenderingOptions();

            _renderer.RenderReportPullRequests(approvedByMe, result.CurrentUserDisplayName, options.Weeks, renderingOptions);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error executing report command: {ex.Message}");
            return 1;
        }
    }
}
