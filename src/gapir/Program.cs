namespace gapir;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Check for help
            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return;
            }

            // Parse command line arguments
            bool showApproved = args.Contains("--show-approved") || args.Contains("-a");
            bool verbose = args.Contains("--verbose") || args.Contains("-v");
            bool useFullUrls = args.Contains("--full-urls") || args.Contains("-f");
            
            // Initialize the logger with verbosity setting
            Log.Initialize(verbose);
            
            // Create and run the checker
            var checker = new PullRequestChecker(showApproved, !useFullUrls);
            await checker.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void ShowHelp()
    {
        Console.WriteLine("gapir (Graph API Review) - Azure DevOps Pull Request Checker");
        Console.WriteLine("===============================================================");
        Console.WriteLine();
        Console.WriteLine("Usage: gapir [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -a, --show-approved    Show table of already approved PRs");
        Console.WriteLine("  -v, --verbose          Show diagnostic messages during execution");
        Console.WriteLine("  -f, --full-urls        Use full Azure DevOps URLs instead of short g URLs");
        Console.WriteLine("  -h, --help             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Description:");
        Console.WriteLine("  Checks for pull requests assigned to you for review in Azure DevOps.");
        Console.WriteLine("  By default, only shows pending PRs. Use --show-approved to also see");
        Console.WriteLine("  a summary table of PRs you have already approved.");
        Console.WriteLine("  Use --verbose to see authentication and operation details.");
        Console.WriteLine("  By default, uses short g URLs. Use --full-urls for complete Azure DevOps URLs.");
    }
}
