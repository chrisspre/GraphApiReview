using System.CommandLine;

namespace gapir;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Create the root command
        var rootCommand = new RootCommand("gapir (Graph API Review) - Azure DevOps Pull Request Checker")
        {
            Description = "Checks for pull requests assigned to you for review in Azure DevOps.\n\n" +
                         "Status Codes (6-char mnemonics):\n" +
                         "  Apprvd  = Approved\n" +
                         "  ApSugg  = Approved with suggestions\n" +
                         "  NoVote  = No vote\n" +
                         "  Wait4A  = Waiting for author\n" +
                         "  Reject  = Rejected\n\n" +
                         "Reason Codes (for approved PRs):\n" +
                         "  Reject  = Rejected\n" +
                         "  Wait4A  = Waiting for author\n" +
                         "  Policy  = Policy/build issues\n" +
                         "  PendRv  = Pending reviewer approval\n" +
                         "  PendOt  = Pending other approvals\n\n" +
                         "Columns Explained:\n" +
                         "  API     = API reviewers approved/assigned (Microsoft Graph API reviewers group)\n" +
                         "  Last By = Who made the most recent activity (Me|Reviewer|Author|Other)\n\n" +
                         "These mnemonics are used in the main table for clarity and alignment."
        };

        // Define options
        var showApprovedOption = new Option<bool>(
            aliases: ["--show-approved", "-a"],
            description: "Show table of already approved PRs");

        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show diagnostic messages during execution");

        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-f"],
            description: "Use full Azure DevOps URLs instead of short g URLs");

        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");

        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        var collectReviewersOption = new Option<bool>(
            aliases: ["--collect-reviewers", "-c"],
            description: "Collect required reviewers from recent PRs and generate ApiReviewersFallback.cs code");

        // Add options to the root command
        rootCommand.AddOption(showApprovedOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(fullUrlsOption);
        rootCommand.AddOption(detailedTimingOption);
        rootCommand.AddOption(showDetailedInfoOption);
        rootCommand.AddOption(collectReviewersOption);

        // Set the handler
        rootCommand.SetHandler(async (showApproved, verbose, fullUrls, detailedTiming, showDetailedInfo, collectReviewers) =>
        {
            try
            {
                // Initialize the logger with verbosity setting
                Log.Initialize(verbose);
                
                if (collectReviewers)
                {
                    // Run the reviewer collection functionality
                    var collector = new ReviewerCollector();
                    await collector.CollectAndGenerateAsync();
                    return;
                }
                
                // Create options object
                var options = new PullRequestCheckerOptions
                {
                    ShowApproved = showApproved,
                    UseShortUrls = !fullUrls,
                    ShowDetailedTiming = detailedTiming,
                    ShowDetailedInfo = showDetailedInfo
                };
                
                // Create and run the checker
                var checker = new PullRequestChecker(options);
                await checker.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Log.Error($"Application error: {ex.Message}");
            }
        }, showApprovedOption, verboseOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption, collectReviewersOption);

        // Invoke the command
        return await rootCommand.InvokeAsync(args);
    }
}
