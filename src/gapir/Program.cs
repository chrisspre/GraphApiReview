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
                         "Status Codes:\n" +
                         "  APP = Approved, APS = Approved with suggestions, NOV = No vote\n" +
                         "  WFA = Waiting for author, REJ = Rejected\n\n" +
                         "Reason Codes (for approved PRs):\n" +
                         "  REJ = Rejected, WFA = Waiting for author, POL = Policy/build issues\n" +
                         "  PRA = Pending reviewer approval, POA = Pending other approvals"
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

        // Add options to the root command
        rootCommand.AddOption(showApprovedOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(fullUrlsOption);
        rootCommand.AddOption(detailedTimingOption);
        rootCommand.AddOption(showDetailedInfoOption);

        // Set the handler
        rootCommand.SetHandler(async (showApproved, verbose, fullUrls, detailedTiming, showDetailedInfo) =>
        {
            try
            {
                // Initialize the logger with verbosity setting
                Log.Initialize(verbose);
                
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
        }, showApprovedOption, verboseOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption);

        // Invoke the command
        return await rootCommand.InvokeAsync(args);
    }
}
