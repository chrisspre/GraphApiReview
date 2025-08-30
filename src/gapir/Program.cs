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
                         "  Sugges  = Approved with suggestions\n" +
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

        // Define global options
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show diagnostic messages during execution");

        var formatOption = new Option<string>(
            aliases: ["--format"],
            getDefaultValue: () => "text",
            description: "Output format: text or json")
        {
            ArgumentHelpName = "FORMAT"
        };

        // Add global options to root command
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(formatOption);

        // Create subcommands
        var reviewCommand = CreateReviewCommand(verboseOption, formatOption);
        var collectReviewersCommand = CreateCollectReviewersCommand(verboseOption, formatOption);
        var diagnosePrCommand = CreateDiagnosePrCommand(verboseOption, formatOption);
        var showApprovedCommand = CreateShowApprovedCommand(verboseOption, formatOption);

        // Add subcommands to root
        rootCommand.AddCommand(reviewCommand);
        rootCommand.AddCommand(collectReviewersCommand);
        rootCommand.AddCommand(diagnosePrCommand);
        rootCommand.AddCommand(showApprovedCommand);

        // Invoke the command
        return await rootCommand.InvokeAsync(args);
    }

    private static Command CreateReviewCommand(Option<bool> verboseOption, Option<string> formatOption)
    {
        var reviewCommand = new Command("review", "Show pull requests assigned to you for review (default command)");

        // Review-specific options
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-f"],
            description: "Use full Azure DevOps URLs instead of short g URLs");

        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");

        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        reviewCommand.AddOption(fullUrlsOption);
        reviewCommand.AddOption(detailedTimingOption);
        reviewCommand.AddOption(showDetailedInfoOption);

        reviewCommand.SetHandler(async (bool verbose, string format, bool fullUrls, bool detailedTiming, bool showDetailedInfo) =>
        {
            Log.Initialize(verbose);
            
            var options = new PullRequestCheckerOptions
            {
                ShowApproved = false,
                UseShortUrls = !fullUrls,
                ShowDetailedTiming = detailedTiming,
                ShowDetailedInfo = showDetailedInfo,
                JsonOutput = format == "json"
            };
            
            var checker = new PullRequestChecker(options);
            await checker.RunAsync();
        }, verboseOption, formatOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption);

        return reviewCommand;
    }

    private static Command CreateCollectReviewersCommand(Option<bool> verboseOption, Option<string> formatOption)
    {
        var collectReviewersCommand = new Command("collect-reviewers", "Collect required reviewers from recent PRs and generate ApiReviewersFallback.cs code");

        collectReviewersCommand.SetHandler(async (bool verbose, string format) =>
        {
            Log.Initialize(verbose);
            
            var collector = new ReviewerCollector();
            await collector.CollectAndGenerateAsync();
        }, verboseOption, formatOption);

        return collectReviewersCommand;
    }

    private static Command CreateDiagnosePrCommand(Option<bool> verboseOption, Option<string> formatOption)
    {
        var diagnosePrCommand = new Command("diagnose-pr", "Diagnose a specific PR ID to show raw reviewer data from Azure DevOps API");

        var prIdArgument = new Argument<int>("pr-id", "The pull request ID to diagnose");

        diagnosePrCommand.AddOption(verboseOption);
        diagnosePrCommand.AddOption(formatOption);
        diagnosePrCommand.AddArgument(prIdArgument);

        diagnosePrCommand.SetHandler(async (bool verbose, string format, int prId) =>
        {
            Log.Initialize(verbose);
            
            var diagnosticChecker = new PullRequestDiagnosticChecker(format == "json");
            await diagnosticChecker.RunAsync(prId);
        }, verboseOption, formatOption, prIdArgument);

        return diagnosePrCommand;
    }

    private static Command CreateShowApprovedCommand(Option<bool> verboseOption, Option<string> formatOption)
    {
        var showApprovedCommand = new Command("approved", "Show table of already approved PRs");

        // approved specific options (same as review)
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-f"],
            description: "Use full Azure DevOps URLs instead of short g URLs");

        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");

        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        showApprovedCommand.AddOption(verboseOption);
        showApprovedCommand.AddOption(formatOption);
        showApprovedCommand.AddOption(fullUrlsOption);
        showApprovedCommand.AddOption(detailedTimingOption);
        showApprovedCommand.AddOption(showDetailedInfoOption);

        showApprovedCommand.SetHandler(async (bool verbose, string format, bool fullUrls, bool detailedTiming, bool showDetailedInfo) =>
        {
            Log.Initialize(verbose);
            
            var options = new PullRequestCheckerOptions
            {
                ShowApproved = true,
                UseShortUrls = !fullUrls,
                ShowDetailedTiming = detailedTiming,
                ShowDetailedInfo = showDetailedInfo,
                JsonOutput = format == "json"
            };
            
            var checker = new PullRequestChecker(options);
            await checker.RunAsync();
        }, verboseOption, formatOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption);

        return showApprovedCommand;
    }
}
