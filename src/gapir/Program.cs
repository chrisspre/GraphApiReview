using System.CommandLine;
using System.Diagnostics;
using gapir.Services;
using gapir.Handlers;
using gapir.Models;
using gapir.Infrastructure;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace gapir;

public class Program
{
    private static async Task<int> Main(string[] args)
    {
        var host = CreateHost();
        var rootCommand = CreateRootCommand(host.Services);
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Creates the host with dependency injection container
    /// </summary>
    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    /// <summary>
    /// Configures dependency injection services
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services from gapir.core
        // Register services
        services.AddSingleton<ConnectionService>();
        services.AddScoped<ConsoleLogger>();
        services.AddScoped<PullRequestDataLoader>();
        services.AddScoped<PullRequestAnalyzer>();
        services.AddScoped<PendingPullRequestService>();
        services.AddScoped<ApprovedPullRequestService>();
        services.AddScoped<PullRequestDiagnostics>();
        services.AddScoped<ReviewerCollector>();
        
        // Command handlers
        services.AddScoped<ReviewCommandHandler>();
        services.AddScoped<ApprovedCommandHandler>();
        services.AddScoped<DiagnoseCommandHandler>();
        services.AddScoped<CollectCommandHandler>();
        services.AddScoped<BaffinoCommandHandler>();
        
        // Rendering services
        services.AddScoped<PullRequestRenderingService>();
        services.AddScoped<UrlGeneratorService>();
        
        // Baffino services
        services.AddScoped<GraphAuthenticationService>();
        services.AddScoped<BaffinoPreferencesService>();
    }

    /// <summary>
    /// Creates the root command with all subcommands and options.
    /// This method is extracted to enable unit testing of the command structure.
    /// </summary>
    public static RootCommand CreateRootCommand(IServiceProvider services)
    {
        // Create the root command
        var rootCommand = new RootCommand("gapir (Graph API Review) - Azure DevOps Pull Request Checker")
        {
            Description = "Tools to manage Graph API Review Pull Requests. Especially to view PRs assigned to you for review.\n\n"
        };

        // Define global options and add them to the root command
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show diagnostic messages during execution");
        rootCommand.AddGlobalOption(verboseOption);

        var formatOption = new Option<Format>(
            aliases: ["--format", "-f"],
            getDefaultValue: () => Format.Text,
            description: "Output format: text or json"
        );
        rootCommand.AddGlobalOption(formatOption);

        // Create subcommands
        AddReviewCommand(rootCommand, verboseOption, formatOption, services);
        AddCollectCommand(rootCommand, verboseOption, formatOption, services);
        AddDiagnoseCommand(rootCommand, verboseOption, formatOption, services);
        AddShowApprovedCommand(rootCommand, verboseOption, formatOption, services);
        AddBaffinoCommand(rootCommand, verboseOption, formatOption, services);

        return rootCommand;
    }

    private static void AddReviewCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var reviewCommand = new Command("review", "Show pull requests assigned to you for review (default command)");

        // Review-specific options
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-u"],
            description: "Use full Azure DevOps URLs instead of shortened http://g/ URLs");

        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");

        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        reviewCommand.AddOption(fullUrlsOption);
        reviewCommand.AddOption(detailedTimingOption);
        reviewCommand.AddOption(showDetailedInfoOption);

        reviewCommand.SetHandler(async (globalOptions, reviewOptions) =>
        {
            var handler = services.GetRequiredService<ReviewCommandHandler>();
            await handler.HandleAsync(reviewOptions, globalOptions);
        },
        new GlobalOptionsBinder(verboseOption, formatOption),
        new ReviewOptionsBinder(fullUrlsOption, detailedTimingOption, showDetailedInfoOption));

        rootCommand.AddCommand(reviewCommand);

        // This is also the default command
        rootCommand.SetHandler(async (globalOptions, reviewOptions) =>
        {
            var handler = services.GetRequiredService<ReviewCommandHandler>();
            await handler.HandleAsync(reviewOptions, globalOptions);
        },
        new GlobalOptionsBinder(verboseOption, formatOption),
        new ReviewOptionsBinder(fullUrlsOption, detailedTimingOption, showDetailedInfoOption));
    }

    private static void AddShowApprovedCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var showApprovedCommand = new Command("approved", "Show table of already approved PRs");

        // Approved specific options
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-u"],
            description: "Use full Azure DevOps URLs instead of short g URLs");

        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");

        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        showApprovedCommand.AddOption(fullUrlsOption);
        showApprovedCommand.AddOption(detailedTimingOption);
        showApprovedCommand.AddOption(showDetailedInfoOption);

        showApprovedCommand.SetHandler(async (globalOptions, approvedOptions) =>
        {
            var handler = services.GetRequiredService<ApprovedCommandHandler>();
            await handler.HandleAsync(approvedOptions, globalOptions);
        },
        new GlobalOptionsBinder(verboseOption, formatOption),
        new ApprovedOptionsBinder(fullUrlsOption, detailedTimingOption, showDetailedInfoOption));

        rootCommand.AddCommand(showApprovedCommand);
    }

    private static void AddDiagnoseCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var diagnoseCommand = new Command("diagnose", "Diagnose a specific PR ID to show raw reviewer data from Azure DevOps API");

        var pullRequestIdArgument = new Argument<int>(
            name: "id",
            description: "The ID of the pull request to diagnose");

        diagnoseCommand.AddArgument(pullRequestIdArgument);

        diagnoseCommand.SetHandler(async (globalOptions, diagnoseOptions) =>
        {
            var handler = services.GetRequiredService<DiagnoseCommandHandler>();
            await handler.HandleAsync(diagnoseOptions, globalOptions);
        },
        new GlobalOptionsBinder(verboseOption, formatOption),
        new DiagnoseOptionsBinder(pullRequestIdArgument));

        rootCommand.AddCommand(diagnoseCommand);
    }

    private static void AddCollectCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var collectCommand = new Command("collect", "Collect required reviewers from recent PRs and generate ApiReviewersFallback.cs code");

        var dryRunOption = new Option<bool>(
            aliases: ["--dry-run", "-n"],
            description: "Show what would be generated without writing to file");

        collectCommand.AddOption(dryRunOption);

        collectCommand.SetHandler(async (globalOptions, collectOptions) =>
        {
            var handler = services.GetRequiredService<CollectCommandHandler>();
            await handler.HandleAsync(collectOptions, globalOptions);
        },
        new GlobalOptionsBinder(verboseOption, formatOption),
        new CollectOptionsBinder(dryRunOption));

        rootCommand.AddCommand(collectCommand);
    }

    private static void AddBaffinoCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var baffinoCommand = new Command("baffino", "Manage Teams Baffino preferences");

        // Create get subcommand
        var getCommand = new Command("get", "Get current Baffino preferences");
        var getFormatOption = new Option<string>(
            name: "--format",
            description: "Output format: table (default), json")
        {
            IsRequired = false
        };
        getFormatOption.SetDefaultValue("table");
        getFormatOption.AddAlias("-f");

        var showAllOption = new Option<bool>(
            name: "--all",
            description: "Show all preferences (default shows only time allocation)")
        {
            IsRequired = false
        };
        showAllOption.AddAlias("-a");

        getCommand.AddOption(getFormatOption);
        getCommand.AddOption(showAllOption);
        getCommand.SetHandler(async (format, showAll, verbose) =>
        {
            var handler = services.GetRequiredService<BaffinoCommandHandler>();
            var result = await handler.HandleGetPreferencesAsync(format, verbose, showAll);
            Environment.Exit(result);
        }, getFormatOption, showAllOption, verboseOption);

        // Create set subcommand
        var setCommand = new Command("set", "Set Baffino preferences");
        var timeAllocationOption = new Option<int>(
            name: "--time-allocation",
            description: "Time allocation value (0-100)")
        {
            IsRequired = true
        };
        timeAllocationOption.AddAlias("-t");

        setCommand.AddOption(timeAllocationOption);
        setCommand.SetHandler(async (timeAllocation, verbose) =>
        {
            var handler = services.GetRequiredService<BaffinoCommandHandler>();
            var result = await handler.HandleSetTimeAllocationAsync(timeAllocation, verbose);
            Environment.Exit(result);
        }, timeAllocationOption, verboseOption);

        // Add subcommands to baffino command
        baffinoCommand.AddCommand(getCommand);
        baffinoCommand.AddCommand(setCommand);

        rootCommand.AddCommand(baffinoCommand);
    }
}
