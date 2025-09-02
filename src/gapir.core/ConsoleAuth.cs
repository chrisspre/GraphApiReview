namespace gapir;

using System.Diagnostics;
using System.Runtime.InteropServices;
using gapir.Services;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;


// Azure Active Directory PowerShell (1b730954-1685-4b74-9bfd-dac224a7b894)

/// <summary>
/// Provides console-based authentication for Azure DevOps using MSAL with brokered authentication support.
/// </summary>
public static class ConsoleAuth
{
    private static readonly string Authority = "https://login.microsoftonline.com/common";
    private static readonly string[] Scopes = ["499b84ac-1321-427f-aa17-267ca6975798/.default"]; // Azure DevOps scope

    /// <summary>
    /// Authenticates with Azure DevOps and returns a VssConnection.
    /// </summary>
    /// <param name="organizationUrl">The Azure DevOps organization URL.</param>
    /// <returns>A VssConnection if authentication succeeds, null otherwise.</returns>
    public static async Task<VssConnection?> AuthenticateAsync()
    {
        try
        {
            Log.Information("Authenticating with Azure DevOps...");

            // Create cache directory for better token persistence
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gapir");
            Directory.CreateDirectory(cacheDir);

            // Create the MSAL client with brokered authentication support
            var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
            var app = PublicClientApplicationBuilder
                .Create(AdoConfig.ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri($"ms-appx-web://microsoft.aad.brokerplugin/{AdoConfig.ClientId}")
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
                    Log.Information("Found cached account, attempting silent authentication...");
                    try
                    {
                        result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();
                        Log.Success("Silent authentication successful using cached token!");
                    }
                    catch (MsalUiRequiredException)
                    {
                        Log.Warning("Cached token expired, attempting interactive authentication...");
                        result = await PerformInteractiveAuthenticationAsync(app);
                    }
                }
                else
                {
                    Log.Information("No cached accounts found, starting interactive authentication...");
                    result = await PerformInteractiveAuthenticationAsync(app);
                }
            }
            catch (MsalUiRequiredException)
            {
                Log.Information("Silent authentication failed, starting interactive authentication...");
                result = await PerformInteractiveAuthenticationAsync(app);
            }

            if (result != null)
            {
                Log.Information("Creating VssConnection with access token...");
                // Create VssConnection with the access token
                var token = new VssAadToken("Bearer", result.AccessToken);
                var credentials = new VssAadCredential(token);
                var connection = new VssConnection(new Uri(AdoConfig.OrganizationUrl), credentials);

                // Test the connection
                Log.Information("Testing connection...");
                await connection.ConnectAsync();
                Log.Success("Connection test successful!");

                return connection;
            }

            Log.Error("Authentication result was null");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Authentication failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Log.Error($"Inner exception: {ex.InnerException.Message}");
            }
            return null;
        }
    }

    private static async Task<AuthenticationResult> PerformInteractiveAuthenticationAsync(IPublicClientApplication app)
    {
        try
        {
            Log.Information("🔐 Attempting brokered authentication (Windows Hello/PIN/Biometrics)...");

            // Try brokered authentication first (best UX)
            var result = await app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithParentActivityOrWindow(GetParentWindow())
                .ExecuteAsync();

            Log.Success("Brokered authentication successful!");
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning($"Brokered authentication failed: {ex.Message}");
            Log.Information("📱 Falling back to device code flow...");
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
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr GetConsoleWindow();
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time

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
            Log.Information("💡 Tip: After first authentication, subsequent runs will use cached tokens!");
            Console.WriteLine("⏳ Waiting for authentication...");

            return Task.FromResult(0);
        }).ExecuteAsync();

        Log.Success("Device code authentication successful!");
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
            Log.Warning($"Could not copy to clipboard: {ex.Message}");
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
            Log.Warning($"Could not open browser automatically: {ex.Message}");
            Console.WriteLine($"Please manually open: {url}");
        }
    }
}
