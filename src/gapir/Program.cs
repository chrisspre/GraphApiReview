using System.CommandLine;
using Microsoft.TeamFoundation.SourceControl.WebApi;

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

        var diagnosticPrOption = new Option<int?>(
            aliases: ["--diagnose-pr"],
            description: "Diagnose a specific PR ID to show raw reviewer data from Azure DevOps API");

        // Add options to the root command
        rootCommand.AddOption(showApprovedOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(fullUrlsOption);
        rootCommand.AddOption(detailedTimingOption);
        rootCommand.AddOption(showDetailedInfoOption);
        rootCommand.AddOption(collectReviewersOption);
        rootCommand.AddOption(diagnosticPrOption);

        // Set the handler
        rootCommand.SetHandler(async (showApproved, verbose, fullUrls, detailedTiming, showDetailedInfo, collectReviewers, diagnosticPr) =>
        {
            try
            {
                // Initialize the logger with verbosity setting
                Log.Initialize(verbose);
                
                if (diagnosticPr.HasValue)
                {
                    // Run diagnostic for specific PR
                    await DiagnosePr(diagnosticPr.Value);
                    return;
                }
                
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
        }, showApprovedOption, verboseOption, fullUrlsOption, detailedTimingOption, showDetailedInfoOption, collectReviewersOption, diagnosticPrOption);

        // Invoke the command
        return await rootCommand.InvokeAsync(args);
    }

    private static async Task DiagnosePr(int prId)
    {
        Console.WriteLine($"Investigating PR {prId} reviewer details...");
        Console.WriteLine("=====================================");
        
        try
        {
            // Use the same authentication as gapir
            var connection = await ConsoleAuth.AuthenticateAsync("https://msazure.visualstudio.com/");
            if (connection == null)
            {
                Console.WriteLine("Authentication failed");
                return;
            }

            var gitClient = connection.GetClient<GitHttpClient>();
            
            // Get repository information the same way as the main tool
            var repository = await gitClient.GetRepositoryAsync("One", "AD-AggregatorService-Workloads");
            
            // Get the specific PR
            var pr = await gitClient.GetPullRequestAsync(repository.Id, prId);
            
            Console.WriteLine($"PR Title: {pr.Title}");
            Console.WriteLine($"PR Status: {pr.Status}");
            Console.WriteLine($"Created By: {pr.CreatedBy?.DisplayName}");
            Console.WriteLine($"Creation Date: {pr.CreationDate}");
            Console.WriteLine($"Total Reviewers Count: {pr.Reviewers?.Count() ?? 0}");
            Console.WriteLine();
            
            if (pr.Reviewers != null && pr.Reviewers.Any())
            {
                Console.WriteLine("REVIEWER DETAILS:");
                Console.WriteLine("================");
                
                foreach (var reviewer in pr.Reviewers)
                {
                    Console.WriteLine($"Reviewer: {reviewer.DisplayName}");
                    Console.WriteLine($"  - Unique Name: {reviewer.UniqueName}");
                    Console.WriteLine($"  - ID: {reviewer.Id}");
                    Console.WriteLine($"  - Vote: {reviewer.Vote} ({GetVoteDescription(reviewer.Vote)})");
                    Console.WriteLine($"  - IsRequired: {reviewer.IsRequired}");
                    Console.WriteLine($"  - IsContainer: {reviewer.IsContainer}");
                    Console.WriteLine($"  - IsFlagged: {reviewer.IsFlagged}");
                    Console.WriteLine();
                }
                
                // Check if the current user is in the reviewers list
                var currentUser = connection.AuthorizedIdentity;
                var myReviewer = pr.Reviewers.FirstOrDefault(r => 
                    r.Id.Equals(currentUser.Id) || 
                    r.DisplayName.Equals(currentUser.DisplayName, StringComparison.OrdinalIgnoreCase));
                    
                if (myReviewer != null)
                {
                    Console.WriteLine("YOUR REVIEWER STATUS:");
                    Console.WriteLine("====================");
                    Console.WriteLine($"Found in reviewers list: YES");
                    Console.WriteLine($"Your Vote: {myReviewer.Vote} ({GetVoteDescription(myReviewer.Vote)})");
                    Console.WriteLine($"IsRequired: {myReviewer.IsRequired}");
                    Console.WriteLine($"IsContainer: {myReviewer.IsContainer}");
                    Console.WriteLine($"IsFlagged: {myReviewer.IsFlagged}");
                }
                else
                {
                    Console.WriteLine("YOUR REVIEWER STATUS: NOT FOUND in reviewers list");
                }
            }
            else
            {
                Console.WriteLine("No reviewers found for this PR");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    private static string GetVoteDescription(int vote)
    {
        return vote switch
        {
            10 => "Approved",
            5 => "Approved with suggestions",
            0 => "No vote",
            -5 => "Waiting for author",
            -10 => "Rejected",
            _ => "Unknown"
        };
    }
}
