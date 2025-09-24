using System.CommandLine;
using System.Diagnostics;
using gapir.Services;
using gapir.Handlers;
using gapir.Models;
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
        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
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
        // Configuration
        services.AddSingleton<AzureDevOpsConfiguration>();
        services.AddSingleton<AuthenticationConfiguration>();
        
        // Core services from gapir.core
        services.AddSingleton<ConsoleAuth>();
        services.AddScoped<PullRequestDiagnosticService>();
        
        // Register services
        services.AddSingleton<ConnectionService>();
        services.AddSingleton<ReviewersConfigurationService>();
        services.AddSingleton<ApiReviewersGroupService>();
        services.AddScoped<PullRequestDataService>();
        services.AddScoped<ConsoleLogger>();
        services.AddScoped<PullRequestDataLoader>();
        services.AddScoped<CompletedPullRequestDataLoader>();
        services.AddScoped<PullRequestAnalyzer>();
        services.AddScoped<PendingPullRequestService>();
        services.AddScoped<ApprovedPullRequestService>();
        services.AddScoped<CompletedPullRequestService>();
        services.AddScoped<PullRequestDiagnostics>();
        services.AddScoped<ReviewerCollector>();
        
        // Command handlers
        services.AddScoped<ReviewCommandHandler>();
        services.AddScoped<ApprovedCommandHandler>();
        services.AddScoped<CompletedCommandHandler>();
        services.AddScoped<DiagnoseCommandHandler>();
        services.AddScoped<CollectCommandHandler>();
        services.AddScoped<PreferencesCommandHandler>();
        
        // Rendering services
        services.AddScoped<PullRequestRenderingService>();
        services.AddScoped<TerminalLinkService>();
        services.AddScoped<GraphAuthenticationService>();
        
        // Reviewer Assignment preferences services
        services.AddScoped<GraphAuthenticationService>();
        services.AddScoped<PreferencesService>();
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
        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Show diagnostic messages during execution"
        };
        rootCommand.Options.Add(verboseOption);

        var formatOption = new Option<Format>("--format", "-f")
        {
            Description = "Output format: text or json",
            DefaultValueFactory = _ => Format.Text
        };
        rootCommand.Options.Add(formatOption);

        // Add default review options to root command
        var detailedTimingOption = new Option<bool>("--detailed-timing", "-t")
        {
            Description = "Show detailed age column - slower due to API calls"
        };

        var showDetailedInfoOption = new Option<bool>("--show-detailed-info", "-d")
        {
            Description = "Show detailed information section for each pending PR"
        };

        rootCommand.Options.Add(detailedTimingOption);
        rootCommand.Options.Add(showDetailedInfoOption);

        // Create subcommands
        AddReviewCommand(rootCommand, verboseOption, formatOption, detailedTimingOption, showDetailedInfoOption, services);
        AddCollectCommand(rootCommand, verboseOption, formatOption, services);
        AddDiagnoseCommand(rootCommand, verboseOption, formatOption, services);
        AddShowApprovedCommand(rootCommand, verboseOption, formatOption, services);
        AddCompletedCommand(rootCommand, verboseOption, formatOption, services);
        AddPreferencesCommand(rootCommand, verboseOption, formatOption, services);

        // This is also the default command
        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<ReviewCommandHandler>();
            var verbose = parseResult.GetValue(verboseOption);
            var format = parseResult.GetValue(formatOption);
            var detailedTiming = parseResult.GetValue(detailedTimingOption);
            var showDetailedInfo = parseResult.GetValue(showDetailedInfoOption);
            var globalOptions = new GlobalOptions(verbose, format);
            var reviewOptions = new ReviewOptions(detailedTiming, showDetailedInfo);
            await handler.HandleAsync(reviewOptions, globalOptions);
            return 0;
        });

        return rootCommand;
    }

    private static void AddReviewCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, Option<bool> detailedTimingOption, Option<bool> showDetailedInfoOption, IServiceProvider services)
    {
        var reviewCommand = new Command("review", "Show pull requests assigned to you for review (default command)");

        reviewCommand.Options.Add(detailedTimingOption);
        reviewCommand.Options.Add(showDetailedInfoOption);

        reviewCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<ReviewCommandHandler>();
            var verbose = parseResult.GetValue(verboseOption);
            var format = parseResult.GetValue(formatOption);
            var detailedTiming = parseResult.GetValue(detailedTimingOption);
            var showDetailedInfo = parseResult.GetValue(showDetailedInfoOption);
            var globalOptions = new GlobalOptions(verbose, format);
            var reviewOptions = new ReviewOptions(detailedTiming, showDetailedInfo);
            await handler.HandleAsync(reviewOptions, globalOptions);
            return 0;
        });

        rootCommand.Subcommands.Add(reviewCommand);
    }

    private static void AddShowApprovedCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var showApprovedCommand = new Command("approved", "Show table of already approved PRs");

        // Approved specific options
        var detailedTimingOption = new Option<bool>("--detailed-timing", "-t")
        {
            Description = "Show detailed age column - slower due to API calls"
        };

        var showDetailedInfoOption = new Option<bool>("--show-detailed-info", "-d")
        {
            Description = "Show detailed information section for each pending PR"
        };

        showApprovedCommand.Options.Add(detailedTimingOption);
        showApprovedCommand.Options.Add(showDetailedInfoOption);

        showApprovedCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<ApprovedCommandHandler>();
            var verbose = parseResult.GetValue(verboseOption);
            var format = parseResult.GetValue(formatOption);
            var detailedTiming = parseResult.GetValue(detailedTimingOption);
            var showDetailedInfo = parseResult.GetValue(showDetailedInfoOption);
            var globalOptions = new GlobalOptions(verbose, format);
            var approvedOptions = new ApprovedOptions(detailedTiming, showDetailedInfo);
            await handler.HandleAsync(approvedOptions, globalOptions);
            return 0;
        });

        rootCommand.Subcommands.Add(showApprovedCommand);
    }

    private static void AddCompletedCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var completedCommand = new Command("completed", "Show table of completed PRs from last N days where you were reviewer (default: 30 days)");

        // Completed specific options
        var detailedTimingOption = new Option<bool>("--detailed-timing", "-t")
        {
            Description = "Show detailed timing information including assignment and closure dates"
        };

        var showDetailedInfoOption = new Option<bool>("--show-detailed-info", "-d")
        {
            Description = "Show detailed information section for each completed PR"
        };

        var daysBackOption = new Option<int>("--days-back", "-b")
        {
            Description = "Number of days to look back for completed PRs (default: 30, max: 90)",
            DefaultValueFactory = _ => 30
        };

        completedCommand.Options.Add(detailedTimingOption);
        completedCommand.Options.Add(showDetailedInfoOption);
        completedCommand.Options.Add(daysBackOption);

        completedCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<CompletedCommandHandler>();
            var verbose = parseResult.GetValue(verboseOption);
            var format = parseResult.GetValue(formatOption);
            var detailedTiming = parseResult.GetValue(detailedTimingOption);
            var showDetailedInfo = parseResult.GetValue(showDetailedInfoOption);
            var daysBack = parseResult.GetValue(daysBackOption);
            
            // Validate days back parameter
            if (daysBack < 1 || daysBack > 90)
            {
                Console.WriteLine("❌ Days back must be between 1 and 90.");
                return 1;
            }
            
            var globalOptions = new GlobalOptions(verbose, format);
            var completedOptions = new CompletedOptions(detailedTiming, showDetailedInfo, daysBack);
            await handler.HandleAsync(completedOptions, globalOptions);
            return 0;
        });

        rootCommand.Subcommands.Add(completedCommand);
    }

    private static void AddDiagnoseCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var diagnoseCommand = new Command("diagnose", "Diagnose a specific PR ID to show raw reviewer data from Azure DevOps API");

        var pullRequestIdArgument = new Argument<int>("id")
        {
            Description = "The ID of the pull request to diagnose"
        };

        diagnoseCommand.Arguments.Add(pullRequestIdArgument);

        diagnoseCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<DiagnoseCommandHandler>();
            var verbose = parseResult.GetValue(verboseOption);
            var format = parseResult.GetValue(formatOption);
            var pullRequestId = parseResult.GetValue(pullRequestIdArgument);
            var globalOptions = new GlobalOptions(verbose, format);
            var diagnoseOptions = new DiagnoseOptions(pullRequestId);
            await handler.HandleAsync(diagnoseOptions, globalOptions);
            return 0;
        });

        rootCommand.Subcommands.Add(diagnoseCommand);
    }

    private static void AddCollectCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var collectCommand = new Command("collect", "Collect required reviewers from recent PRs and generate JSON configuration");

        var dryRunOption = new Option<bool>("--dry-run", "-n")
        {
            Description = "Show what would be generated without writing to file"
        };

        collectCommand.Options.Add(dryRunOption);

        collectCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<CollectCommandHandler>();
            var verbose = parseResult.GetValue(verboseOption);
            var format = parseResult.GetValue(formatOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var globalOptions = new GlobalOptions(verbose, format);
            var collectOptions = new CollectOptions(dryRun);
            await handler.HandleAsync(collectOptions, globalOptions);
            return 0;
        });

        rootCommand.Subcommands.Add(collectCommand);
    }

    private static void AddPreferencesCommand(RootCommand rootCommand, Option<bool> verboseOption, Option<Format> formatOption, IServiceProvider services)
    {
        var preferencesCommand = new Command("preferences", "Manage Reviewer Assignment preferences");

        // Create get subcommand
        var getCommand = new Command("get", "Get current Reviewer Assignment preferences");
        var getFormatOption = new Option<string>("--format", "-f")
        {
            Description = "Output format: table (default), json",
            DefaultValueFactory = _ => "table"
        };

        var showAllOption = new Option<bool>("--all", "-a")
        {
            Description = "Show all preferences (default shows only time allocation)"
        };

        getCommand.Options.Add(getFormatOption);
        getCommand.Options.Add(showAllOption);
        getCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<PreferencesCommandHandler>();
            var format = parseResult.GetValue(getFormatOption);
            var showAll = parseResult.GetValue(showAllOption);
            var verbose = parseResult.GetValue(verboseOption);
            var result = await handler.HandleGetPreferencesAsync(format, verbose, showAll);
            Environment.Exit(result);
        });

        // Create set subcommand
        var setCommand = new Command("set", "Set Reviewer Assignment Preferences");
        var timeAllocationOption = new Option<int>("--time-allocation", "-t")
        {
            Description = "Time allocation value (0-100)"
        };

        setCommand.Options.Add(timeAllocationOption);
        setCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var handler = services.GetRequiredService<PreferencesCommandHandler>();
            var timeAllocation = parseResult.GetValue(timeAllocationOption);
            var verbose = parseResult.GetValue(verboseOption);
            var result = await handler.HandleSetTimeAllocationAsync(timeAllocation, verbose);
            Environment.Exit(result);
        });

        // Add subcommands to preferences command
        preferencesCommand.Subcommands.Add(getCommand);
        preferencesCommand.Subcommands.Add(setCommand);

        rootCommand.Subcommands.Add(preferencesCommand);
    }
}
