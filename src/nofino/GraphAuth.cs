namespace nofino;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Graph;
using System.Net.Http.Headers;

/// <summary>
/// Provides console-based authentication for Microsoft Graph using MSAL with brokered authentication support.
/// </summary>
public static class GraphAuth
{
    // Baffino Teams Bot App ID - should have the necessary permissions for managing baffino extensions
    private static readonly string ClientId = "61b9d41c-81e2-4fc5-8f77-dcb71f48022c"; 
    private static readonly string Authority = "https://login.microsoftonline.com/common";
    private static readonly string[] Scopes = ["https://graph.microsoft.com/User.ReadWrite"]; // Full permissions for extension management

    /// <summary>
    /// Authenticates with Microsoft Graph and returns a GraphServiceClient.
    /// </summary>
    /// <returns>A GraphServiceClient if authentication succeeds, null otherwise.</returns>
    public static async Task<GraphServiceClient?> AuthenticateAsync()
    {
        try
        {
            Console.WriteLine("Authenticating with Microsoft Graph...");
            Console.WriteLine("🤖 Using Baffino Teams Bot app ID - should have proper extension permissions");

            // Create cache directory for better token persistence
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "nofino");
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
                Console.WriteLine("Creating GraphServiceClient with access token...");
                
                // Create GraphServiceClient with custom HttpClient that includes the auth header
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", result.AccessToken);
                
                var graphClient = new GraphServiceClient(httpClient);

                // Test the connection
                Console.WriteLine("Testing connection...");
                var me = await graphClient.Me.GetAsync();
                Console.WriteLine($"Connection test successful! Authenticated as: {me?.DisplayName} ({me?.UserPrincipalName})");

                return graphClient;
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
            
            // Check for admin consent errors
            if (ex.Message.Contains("AADSTS65001") || ex.Message.Contains("admin consent"))
            {
                Console.WriteLine();
                Console.WriteLine("🚨 Admin Consent Required!");
                Console.WriteLine("This error typically means:");
                Console.WriteLine("   1. Your organization requires admin approval for this application");
                Console.WriteLine("   2. The requested permissions need admin consent");
                Console.WriteLine("   3. You may need to use a personal Microsoft account instead");
                Console.WriteLine();
                Console.WriteLine("💡 Possible solutions:");
                Console.WriteLine("   - Ask your Azure AD admin to approve the Microsoft Graph PowerShell app");
                Console.WriteLine("   - Try using a personal Microsoft account (outlook.com, hotmail.com, etc.)");
                Console.WriteLine("   - Use Azure CLI instead: az login && az rest --method get --url 'https://graph.microsoft.com/v1.0/me'");
            }
            
            return null;
        }
    }

    private static async Task<AuthenticationResult> PerformInteractiveAuthenticationAsync(IPublicClientApplication app)
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

    private static IntPtr GetParentWindow()
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

    private static async Task<AuthenticationResult> PerformDeviceCodeFlowAsync(IPublicClientApplication app)
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

    private static string ExtractDeviceCode(string message)
    {
        // Extract device code from message like "...enter the code ABC123DEF to authenticate."
        var codeStart = message.IndexOf("enter the code ");
        if (codeStart == -1) return string.Empty;

        codeStart += "enter the code ".Length;
        var codeEnd = message.IndexOf(" to authenticate", codeStart);
        if (codeEnd == -1) return string.Empty;

        return message.Substring(codeStart, codeEnd - codeStart);
    }

    private static string ExtractUrl(string message)
    {
        // Extract URL from message like "To sign in, use a web browser to open the page https://..."
        var urlStart = message.IndexOf("https://");
        if (urlStart == -1) return string.Empty;

        var urlEnd = message.IndexOf(' ', urlStart);
        if (urlEnd == -1) urlEnd = message.Length;

        return message[urlStart..urlEnd];
    }

    private static void CopyToClipboard(string text)
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

    private static void OpenBrowser(string url)
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
