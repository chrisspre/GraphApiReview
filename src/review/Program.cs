using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    // Azure CLI App ID for Device Code Flow 
    private static readonly string ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46"; // Azure CLI Client ID

    private static readonly string Authority = "https://login.microsoftonline.com/common";

    private static readonly string[] Scopes = ["499b84ac-1321-427f-aa17-267ca6975798/.default"]; // Azure DevOps scope

    static async Task Main(string[] args)
    {
        try
        {
            // Check for full-urls flag
            bool useFullUrls = args.Contains("--full-urls");
            
            // Check for URL shortening flag
            bool useUrlShortening = args.Contains("--short-urls");
            
            // PR checker functionality
            await RunPRCheckerAsync(useFullUrls, useUrlShortening);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    static async Task RunPRCheckerAsync(bool useFullUrls = false, bool useUrlShortening = false)
    {
        // Azure DevOps organization URL
        var organizationUrl = "https://msazure.visualstudio.com/";
        
        // Project and repository details
        var projectName = "One";
        var repositoryName = "AD-AggregatorService-Workloads";
        
        Console.WriteLine("gapir (Graph API Review) - Azure DevOps Pull Request Checker");
        Console.WriteLine("===============================================================");
        if (useUrlShortening)
        {
            Console.WriteLine("Note: URLs shown as shortened links (e.g., 'http://aka.ms/gapir?id=123456'). Use --full-urls for complete URLs.");
        }
        else if (!useFullUrls)
        {
            Console.WriteLine("Note: URLs shown as standard links. Use --short-urls for shortened URLs or --full-urls for complete URLs.");
        }
        Console.WriteLine("Full URLs: https://msazure.visualstudio.com/One/_git/AD-AggregatorService-Workloads/pullrequest/{ID}");
        Console.WriteLine();
        
        // Authenticate using Visual Studio credentials or prompt for PAT
        var connection = await AuthenticateAsync(organizationUrl);
        
        if (connection == null)
        {
            Console.WriteLine("Authentication failed. Exiting...");
            return;
        }
        
        Console.WriteLine("Successfully authenticated!");
        
        // Get pull requests assigned to the current user
        await CheckPullRequestsAsync(connection, projectName, repositoryName, useFullUrls, useUrlShortening);
    }

    static async Task<VssConnection?> AuthenticateAsync(string organizationUrl)
    {
        try
        {
            Console.WriteLine("Authenticating with Azure DevOps...");
            
            // Create cache directory for better token persistence
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzureDevOpsPRChecker");
            Directory.CreateDirectory(cacheDir);
            
            // Create the MSAL client with brokered authentication support
            var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
            var app = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri("http://localhost")
                .WithBroker(brokerOptions) // Enable brokered authentication (WAM)
                .Build();

            // Configure token cache for better persistence
            var cacheHelper = new TokenCacheHelper(cacheDir);
            cacheHelper.EnableSerialization(app.UserTokenCache);

            AuthenticationResult? result = null;
            
            try
            {
                // Try to get token silently first (from cache)
                var accounts = await app.GetAccountsAsync();
                if (accounts.Any())
                {
                    Console.WriteLine("Found cached account, attempting silent authentication...");
                    try
                    {
                        result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();
                        Console.WriteLine("✅ Silent authentication successful using cached token!");
                    }
                    catch (MsalUiRequiredException)
                    {
                        Console.WriteLine("⚠️  Cached token expired, attempting interactive authentication...");
                        result = await PerformInteractiveAuthenticationAsync(app);
                    }
                }
                else
                {
                    Console.WriteLine("No cached accounts found, starting interactive authentication...");
                    result = await PerformInteractiveAuthenticationAsync(app);
                }
            }
            catch (MsalUiRequiredException)
            {
                Console.WriteLine("Silent authentication failed, starting interactive authentication...");
                result = await PerformInteractiveAuthenticationAsync(app);
            }

            if (result != null)
            {
                Console.WriteLine("Creating VssConnection with access token...");
                // Create VssConnection with the access token
                var token = new VssAadToken("Bearer", result.AccessToken);
                var credentials = new VssAadCredential(token);
                var connection = new VssConnection(new Uri(organizationUrl), credentials);
                
                // Test the connection
                Console.WriteLine("Testing connection...");
                await connection.ConnectAsync();
                Console.WriteLine("Connection test successful!");
                
                return connection;
            }
            
            Console.WriteLine("Authentication result was null");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            return null;
        }
    }
    
    static async Task<AuthenticationResult> PerformInteractiveAuthenticationAsync(IPublicClientApplication app)
    {
        try
        {
            Console.WriteLine("🔐 Attempting brokered authentication (Windows Hello/PIN/Biometrics)...");
            
            // Try brokered authentication first (best UX)
            var result = await app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithParentActivityOrWindow(GetParentWindow())
                .ExecuteAsync();
                
            Console.WriteLine("✅ Brokered authentication successful!");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Brokered authentication failed: {ex.Message}");
            Console.WriteLine("📱 Falling back to device code flow...");
            return await PerformDeviceCodeFlowAsync(app);
        }
    }
    
    static IntPtr GetParentWindow()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Try to get the console window handle
                return GetConsoleWindow();
            }
            catch
            {
                // If that fails, return IntPtr.Zero (no parent window)
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    
    static async Task<AuthenticationResult> PerformDeviceCodeFlowAsync(IPublicClientApplication app)
    {
        var result = await app.AcquireTokenWithDeviceCode(Scopes, deviceCodeResult =>
        {
            Console.WriteLine(deviceCodeResult.Message);
            Console.WriteLine();
            
            // Extract the device code from the message
            var deviceCode = ExtractDeviceCode(deviceCodeResult.Message);
            var url = ExtractUrl(deviceCodeResult.Message);
            
            if (!string.IsNullOrEmpty(deviceCode))
            {
                // Copy device code to clipboard
                CopyToClipboard(deviceCode);
                Console.WriteLine($"✅ Device code '{deviceCode}' has been copied to your clipboard!");
            }
            
            if (!string.IsNullOrEmpty(url))
            {
                // Open browser automatically
                OpenBrowser(url);
                Console.WriteLine($"✅ Browser opened automatically to: {url}");
            }
            
            Console.WriteLine("📋 Simply paste the code (Ctrl+V) in the browser and sign in.");
            Console.WriteLine("💡 Tip: After first authentication, subsequent runs will use cached tokens!");
            Console.WriteLine("⏳ Waiting for authentication...");
            
            return Task.FromResult(0);
        }).ExecuteAsync();
        
        Console.WriteLine("Device code authentication successful!");
        return result;
    }
    
    static async Task CheckPullRequestsAsync(VssConnection connection, string projectName, string repositoryName, bool useFullUrls, bool useUrlShortening)
    {
        try
        {
            // Get Git client
            var gitClient = connection.GetClient<GitHttpClient>();
            
            // Get current user identity
            var currentUser = connection.AuthorizedIdentity;
            
            Console.WriteLine($"Checking pull requests for user: {currentUser.DisplayName}");
            
            // Get repository
            var repository = await gitClient.GetRepositoryAsync(projectName, repositoryName);
            
            // Search for pull requests assigned to the current user
            var searchCriteria = new GitPullRequestSearchCriteria()
            {
                Status = PullRequestStatus.Active,
                ReviewerId = currentUser.Id
            };
            
            var pullRequests = await gitClient.GetPullRequestsAsync(repository.Id, searchCriteria);
            
            // Separate PRs into approved and pending
            var approvedPullRequests = new List<GitPullRequest>();
            var pendingPullRequests = new List<GitPullRequest>();
            
            foreach (var pr in pullRequests)
            {
                var currentUserReviewer = pr.Reviewers?.FirstOrDefault(r => 
                    r.Id.Equals(currentUser.Id) || 
                    r.DisplayName.Equals(currentUser.DisplayName, StringComparison.OrdinalIgnoreCase));
                
                var isApproved = currentUserReviewer != null && currentUserReviewer.Vote == 10;
                
                if (isApproved)
                    approvedPullRequests.Add(pr);
                else
                    pendingPullRequests.Add(pr);
            }
            
            // Show short list of approved PRs
            if (approvedPullRequests.Count > 0)
            {
                Console.WriteLine($"\n✅ {approvedPullRequests.Count} PR(s) you have already approved:");
                
                // Prepare table data
                var headers = new[] { "Author", "Title", "URL" };
                var maxWidths = new[] { 25, 45, 30 }; // Maximum column widths
                
                var rows = new List<string[]>();
                foreach (var pr in approvedPullRequests)
                {
                    var url = useFullUrls ? GetFullPullRequestUrl(pr, projectName, repositoryName) : 
                              useUrlShortening ? GetShortPullRequestUrl(pr, projectName, repositoryName) : 
                              GetFullPullRequestUrl(pr, projectName, repositoryName);
                    
                    rows.Add(new[] { pr.CreatedBy.DisplayName, ShortenTitle(pr.Title), url });
                }
                
                PrintTable(headers, rows, maxWidths);
            }
            
            // Show detailed list of pending PRs
            Console.WriteLine($"\n⏳ {pendingPullRequests.Count} PR(s) pending your approval:");
            Console.WriteLine(new string('=', 60));
            
            if (pendingPullRequests.Count == 0)
            {
                Console.WriteLine("No pull requests found pending your approval.");
                return;
            }
            
            foreach (var pr in pendingPullRequests)
            {
                Console.WriteLine($"ID: {pr.PullRequestId}");
                Console.WriteLine($"Title: {ShortenTitle(pr.Title)}");
                Console.WriteLine($"Author: {pr.CreatedBy.DisplayName}");
                Console.WriteLine($"Status: {pr.Status}");
                Console.WriteLine($"Created: {pr.CreationDate:yyyy-MM-dd HH:mm:ss}");                
                Console.WriteLine($"URL: {(useFullUrls ? GetFullPullRequestUrl(pr, projectName, repositoryName) : useUrlShortening ? GetShortPullRequestUrl(pr, projectName, repositoryName) : GetFullPullRequestUrl(pr, projectName, repositoryName))}");
                
                // Check if there are any reviewers (filter out groups and automation accounts)
                if (pr.Reviewers?.Any() == true)
                {
                    var humanReviewers = pr.Reviewers.Where(r => 
                        !r.DisplayName.StartsWith("[TEAM FOUNDATION]") && 
                        !r.DisplayName.StartsWith("[One]") &&
                        !r.DisplayName.Equals("Ownership Enforcer", StringComparison.OrdinalIgnoreCase) &&
                        !r.DisplayName.Contains("Bot", StringComparison.OrdinalIgnoreCase) &&
                        !r.DisplayName.Contains("Automation", StringComparison.OrdinalIgnoreCase)
                    ).ToList();
                    
                    if (humanReviewers.Any())
                    {
                        Console.WriteLine("Reviewers:");
                        foreach (var reviewer in humanReviewers)
                        {
                            var vote = reviewer.Vote switch
                            {
                                10 => "Approved",
                                5 => "Approved with suggestions",
                                0 => "No vote",
                                -5 => "Waiting for author",
                                -10 => "Rejected",
                                _ => "Unknown"
                            };
                            Console.WriteLine($"  - {reviewer.DisplayName}: {vote}");
                        }
                    }
                }
                
                Console.WriteLine(new string('-', 60));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking pull requests: {ex.Message}");
        }
    }
    
    static string GetShortPullRequestUrl(GitPullRequest pr, string projectName, string repositoryName)
    {
        // Use Microsoft's aka.ms service with query parameter support for shortened URLs
        // Since aka.ms doesn't support path parameters like /gapir/123456, we use query parameters
        // Setup required:
        // 1. Go to https://aka.ms (Microsoft's URL shortener service)
        // 2. Create a short URL: gapir -> https://msazure.visualstudio.com/One/_git/AD-AggregatorService-Workloads/pullrequest/
        // 3. This creates working short URLs: aka.ms/gapir?id=123456 -> full URL + query parameter
        // 4. Benefits: Official Microsoft service, supports query parameters, reliable
        
        return $"http://aka.ms/gapir?id={pr.PullRequestId}";
        
        // Alternative options if aka.ms doesn't work:
        // return $"http://bit.ly/azdo-pr?id={pr.PullRequestId}";       // If using Bit.ly with query parameters
        // return $"http://tinyurl.com/azdo-pr?id={pr.PullRequestId}";  // If using TinyURL with query parameters
    }
    
    static string GetPullRequestUrl(GitPullRequest pr, string projectName, string repositoryName)
    {
        // This method is kept for backward compatibility but now just returns the full URL
        // since URL shortening is now behind a feature flag
        return GetFullPullRequestUrl(pr, projectName, repositoryName);
    }
    
    static string GetFullPullRequestUrl(GitPullRequest pr, string projectName, string repositoryName)
    {
        // Keep the original method available for when full URLs are needed
        return $"https://msazure.visualstudio.com/{projectName}/_git/{repositoryName}/pullrequest/{pr.PullRequestId}";
    }
    
    static string ShortenTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return title;
        
        // Remove all dashes and replace with spaces
        var cleaned = title.Replace("-", " ");
        
        // Remove trailing numbers (like issue numbers)
        // Pattern: removes numbers at the end, optionally preceded by spaces or special chars
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"^\s*\d+\s*", "");
        
        // Remove extra whitespace
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();
        
        // Truncate to 40 characters
        if (cleaned.Length > 40)
        {
            cleaned = $"{cleaned[..37]}...";
        }
        
        return cleaned;
    }
    static void PrintTable(string[] headers, IReadOnlyCollection<string[]> rows, int[] maxWidths)
    {
        if (headers == null || rows == null || maxWidths == null)
            return;
        
        if (headers.Length != maxWidths.Length)
            throw new ArgumentException("Headers and maxWidths arrays must have the same length.");
        
        // Calculate actual column widths based on content
        var actualWidths = new int[headers.Length];
        
        // Start with header lengths
        for (int i = 0; i < headers.Length; i++)
        {
            actualWidths[i] = Math.Min(headers[i].Length, maxWidths[i]);
        }
        
        // Check content lengths
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows
                
            for (int i = 0; i < row.Length && i < actualWidths.Length; i++)
            {
                var contentLength = row[i]?.Length ?? 0;
                actualWidths[i] = Math.Max(actualWidths[i], Math.Min(contentLength, maxWidths[i]));
            }
        }
        
        // Print header
        Console.WriteLine();
        for (int i = 0; i < headers.Length; i++)
        {
            var header = TruncateString(headers[i], actualWidths[i]);
            Console.Write($"{header.PadRight(actualWidths[i])}");
            if (i < headers.Length - 1)
                Console.Write(" | ");
        }
        Console.WriteLine();
        
        // Print separator
        for (int i = 0; i < headers.Length; i++)
        {
            Console.Write(new string('-', actualWidths[i]));
            if (i < headers.Length - 1)
                Console.Write("-+-");
        }
        Console.WriteLine();
        
        // Print rows
        foreach (var row in rows)
        {
            if (row.Length != headers.Length)
                continue; // Skip malformed rows
                
            for (int i = 0; i < row.Length; i++)
            {
                var content = TruncateString(row[i] ?? "", actualWidths[i]);
                Console.Write($"{content.PadRight(actualWidths[i])}");
                if (i < row.Length - 1)
                    Console.Write(" | ");
            }
            Console.WriteLine();
        }
    }
    
    static string TruncateString(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
            
        if (text.Length <= maxLength)
            return text;
            
        return $"{text[..(maxLength-3)]}...";
    }
    
    static string ExtractDeviceCode(string message)
    {
        // Extract device code from message like "...enter the code ABC123DEF to authenticate."
        var codeStart = message.IndexOf("enter the code ");
        if (codeStart == -1) return string.Empty;
        
        codeStart += "enter the code ".Length;
        var codeEnd = message.IndexOf(" to authenticate", codeStart);
        if (codeEnd == -1) return string.Empty;
        
        return message.Substring(codeStart, codeEnd - codeStart);
    }
    
    static string ExtractUrl(string message)
    {
        // Extract URL from message like "To sign in, use a web browser to open the page https://..."
        var urlStart = message.IndexOf("https://");
        if (urlStart == -1) return string.Empty;
        
        var urlEnd = message.IndexOf(" ", urlStart);
        if (urlEnd == -1) urlEnd = message.Length;
        
        return message.Substring(urlStart, urlEnd - urlStart);
    }
    
    static void CopyToClipboard(string text)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use PowerShell to copy to clipboard on Windows
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Set-Clipboard -Value '{text}'\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit();
            }
            else
            {
                Console.WriteLine($"⚠️  Auto-copy not supported on this platform. Manual copy needed: {text}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not copy to clipboard: {ex.Message}");
            Console.WriteLine($"Manual copy needed: {text}");
        }
    }
    
    static void OpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not open browser automatically: {ex.Message}");
            Console.WriteLine($"Please manually open: {url}");
        }
    }
}
