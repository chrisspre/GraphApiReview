using System.CommandLine;

namespace gapir;

public class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = CreateRootCommand();
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Creates the root command with all subcommands and options.
    /// This method is extracted to enable unit testing of the command structure.
    /// </summary>
    public static RootCommand CreateRootCommand()
    {
        // Create the root command
        var rootCommand = new RootCommand("gapir (Graph API Review) - Azure DevOps Pull Request Checker")
        {
            Description = $"Tools to manage Graph API Review Pull Requests. Especially to view PRs assigned to you for review.\n\n"
        };

        // Define global options
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show diagnostic messages during execution");

        var formatOption = new Option<Format>(
            aliases: ["--format", "-f"],
            getDefaultValue: () => Format.Text,
            description: "Output format: text or json"
        );

        // Add global options to root command
        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(formatOption);

        // Create subcommands
        AddReviewCommand(rootCommand, verboseOption, formatOption);
        AddCollectCommand(rootCommand, verboseOption, formatOption);
        AddDiagnoseCommand(rootCommand, verboseOption, formatOption);
        AddShowApprovedCommand(rootCommand, verboseOption, formatOption);

        return rootCommand;
    }

    private static void AddReviewCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption)
    {
        var reviewCommand = new Command("review", "Show pull requests assigned to you for review (default command)");

        // Review-specific options
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-u"],
            description: "Use full Azure DevOps URLs instead of shortend http://g/ URLs");

        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");

        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        reviewCommand.AddOption(fullUrlsOption);
        reviewCommand.AddOption(detailedTimingOption);
        reviewCommand.AddOption(showDetailedInfoOption);

        reviewCommand.SetHandler(ReviewCommandHandler, verboseOption, formatOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption);

        rootCommand.AddCommand(reviewCommand);

        // this is also the default command
        rootCommand.SetHandler(ReviewCommandHandler, verboseOption, formatOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption);
    }

    internal static async Task ReviewCommandHandler(bool verbose, Format format, bool fullUrls, bool detailedTiming, bool showDetailedInfo)
    {
        Log.Initialize(verbose);

        var options = new PullRequestCheckerOptions
        {
            ShowApproved = false,
            UseShortUrls = !fullUrls,
            ShowDetailedTiming = detailedTiming,
            ShowDetailedInfo = showDetailedInfo,
            Format = format
        };

        var checker = new PullRequestChecker(options);
        await checker.RunAsync();
    }


    private static void AddCollectCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption)
    {
        var collectCommand = new Command("collect", "Collect required reviewers from recent PRs and generate ApiReviewersFallback.cs code");

        collectCommand.SetHandler(CollectCommandHandler, verboseOption, formatOption);

        rootCommand.AddCommand(collectCommand);
    }

    private static async Task CollectCommandHandler(bool verbose, Format format)
    {
        Log.Initialize(verbose);

        var collector = new ReviewerCollector();
        await collector.CollectAndGenerateAsync();
    }

    private static void AddDiagnoseCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption)
    {
        var diagnoseCommand = new Command("diagnose", "Diagnose a specific PR ID to show raw reviewer data from Azure DevOps API");

        var prIdArgument = new Argument<int>("id", "The pull request ID to diagnose");

        diagnoseCommand.AddOption(verboseOption);
        diagnoseCommand.AddOption(formatOption);
        diagnoseCommand.AddArgument(prIdArgument);

        diagnoseCommand.SetHandler(DiagnoseCommandHandler, verboseOption, formatOption, prIdArgument);

        rootCommand.AddCommand(diagnoseCommand);
    }

    private static async Task DiagnoseCommandHandler(bool verbose, Format format, int prId)
    {
        Log.Initialize(verbose);

        var diagnosticChecker = new PullRequestDiagnosticChecker(format);
        await diagnosticChecker.RunAsync(prId);
    }

    private static void AddShowApprovedCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption)
    {
        var showApprovedCommand = new Command("approved", "Show table of already approved PRs");

        // approved specific options (same as review)
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-u"],
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


        showApprovedCommand.SetHandler(ShowApprovedCommandHandler, verboseOption, formatOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption);

        rootCommand.AddCommand(showApprovedCommand);
    }

    private static async Task ShowApprovedCommandHandler(bool verbose, Format format, bool fullUrls, bool detailedTiming, bool showDetailedInfo)
    {
        Log.Initialize(verbose);

        var options = new PullRequestCheckerOptions
        {
            ShowApproved = true,
            UseShortUrls = !fullUrls,
            ShowDetailedTiming = detailedTiming,
            ShowDetailedInfo = showDetailedInfo,
            Format = format
        };

        var checker = new PullRequestChecker(options);
        await checker.RunAsync();
    }
}
